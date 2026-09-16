using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.DependencyResolvers.Autofac;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Sentry.Extensibility;
using Serilog;
using Serilog.Events;

// Konteyner sağlık denetimi (docker-compose.prod.yml): uygulama kurulmadan tek istek atıp çıkar.
if (args.Contains(HealthProbe.Argument))
{
    Environment.Exit(await HealthProbe.RunAsync());
}

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container => container.RegisterModule(new AutofacBusinessModule()));

// Konsol + 14 gün tutulan günlük dosya; telefon/e-posta/adres PiiMaskEnricher ile maskelenir.
// DI'daki ek sink'ler (testlerin bellek sink'i) ReadFrom.Services ile bağlanır; DI'a sonradan eklenen
// ILoggerProvider'lar (testlerin sorgu sayacı) writeToProviders ile beslenir. Varsayılan konsol sağlayıcısı
// Serilog konsoluyla çift yazmasın diye temizlenir. Sentry:Dsn doluysa hatalar Sentry'ye de gider (SentryReporting).
builder.Logging.ClearProviders();
builder.Services.AddSerilog((services, logger) =>
    {
        var sentryDsn = services.GetRequiredService<IConfiguration>()["Sentry:Dsn"];
        logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<PiiMaskEnricher>()
            .WriteTo.Console()
            .WriteTo.File("logs/heryerde-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .ReadFrom.Services(services);
        if (SentryReporting.Enabled(sentryDsn))
        {
            logger.WriteTo.Sentry(options => SentryReporting.Configure(options, sentryDsn!, services.GetService<ITransport>()));
        }
    },
    preserveStaticLogger: true,
    writeToProviders: true);

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new InvariantDecimalModelBinderProvider());
    options.ModelBinderProviders.Insert(0, new CheckboxBoolModelBinderProvider());
    // Tek bir POST'ta bile unutulmasın diye antiforgery doğrulaması genelde açık.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Türkçe harfler HTML kaynağında entity'ye çevrilmesin.
builder.Services.AddSingleton(HtmlEncoder.Create(
    UnicodeRanges.BasicLatin,
    UnicodeRanges.Latin1Supplement,
    UnicodeRanges.LatinExtendedA));

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
builder.Services.Configure<ShopSettings>(builder.Configuration.GetSection("Shop"));
builder.Services.Configure<NotificationSettings>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<ShippingSettings>(builder.Configuration.GetSection("Shipping"));
builder.Services.Configure<IyzicoSettings>(builder.Configuration.GetSection("Iyzico"));

builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection("RateLimit"));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(RateLimitPolicy.Select);
});

// Metin yanıtları br, yoksa gzip ile sıkışır; TLS altında da açık (statik varlıklar zaten sürümlü).
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes =
    [
        "text/html",
        "text/css",
        "text/javascript",
        "application/javascript",
        "application/json",
        "image/svg+xml",
        "application/xml",
        "text/xml"
    ];
});

builder.Services.AddOutputCache();

builder.Services.AddDbContext<HerYerdeContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHealthChecks().AddCheck<ReadinessCheck>("hazirlik", tags: [ReadinessCheck.Tag]);

// TLS'i sonlandıran vekil arkasında şema ve istemci IP'si başlıktan okunur. Başlık yalnız tanımlı vekilden
// gelirse geçerlidir: liste boşken middleware her kaynağa güvenirdi ve X-Forwarded-For ile hız sınırı atlatılırdı.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }

    options.ForwardedHeaders = options.KnownIPNetworks.Count + options.KnownProxies.Count > 0
        ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        : ForwardedHeaders.None;
});

// Üretimde çerezler her zaman Secure; geliştirmede http ile çalışılabilsin diye istekle aynı.
builder.Services.Configure<CookiePolicyOptions>(options => options.Secure = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always);
builder.Services.AddSingleton(TimeProvider.System);
// 81 il ve ilçeleri bir kez okunur; ödeme formundaki bağımlı seçim buradan beslenir.
builder.Services.AddSingleton<IProvinceDirectory>(services =>
    new ProvinceDirectory(services.GetRequiredService<IWebHostEnvironment>().WebRootPath));

// Yüklenen görseller varsayılan olarak wwwroot altına yazılır; testler Uploads:Root ile başka yere yönlendirir.
builder.Services.AddSingleton<IProductImageStorage>(services => new ProductImageStorage(
    builder.Configuration["Uploads:Root"] is { Length: > 0 } uploadsRoot
        ? uploadsRoot
        : services.GetRequiredService<IWebHostEnvironment>().WebRootPath));
// Video yüklenince sunucuda 720p mp4 + poster + önizleme üretilir; ffmpeg PATH'te değilse Media:FfmpegPath verilir.
builder.Services.AddSingleton<IProductVideoStorage>(services => new FfmpegVideoStorage(
    builder.Configuration["Uploads:Root"] is { Length: > 0 } videoRoot
        ? videoRoot
        : services.GetRequiredService<IWebHostEnvironment>().WebRootPath,
    builder.Configuration["Media:FfmpegPath"]));
// Fatura ve dekontlar wwwroot dışında: statik dosya ara katmanı onlara hiç ulaşmaz.
builder.Services.AddSingleton<IPrivateFileStorage>(services => new PrivateFileStorage(
    builder.Configuration["PrivateFiles:Root"] is { Length: > 0 } privateRoot
        ? privateRoot
        : Path.Combine(services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "private")));
builder.Services.AddHostedService<CartCleanupHostedService>();
builder.Services.AddHostedService<AuditLogCleanupHostedService>();
builder.Services.AddHostedService<PersonalDataCleanupHostedService>();
builder.Services.AddSingleton<ILegalPdfArchive, LegalPdfArchive>();
builder.Services.AddSingleton<INotificationSender, SmtpNotificationSender>();
builder.Services.AddHostedService<OutboxHostedService>();
builder.Services.AddHostedService<ReviewInviteHostedService>();
builder.Services.AddHttpClient<IPaymentProvider, IyzicoPaymentProvider>(client => client.Timeout = TimeSpan.FromSeconds(30));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "heryerde.admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/admin/auth/login";
        options.LogoutPath = "/admin/auth/logout";
        options.AccessDeniedPath = "/admin/auth/login";
        // 12 saat, kullanıldıkça uzar: gün boyu açık panel düşmez, unutulmuş tarayıcı ertesi gün kapalıdır.
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        // Parola değiştiğinde damga ilerler; eski damgayı taşıyan çerezler ilk istekte düşer.
        options.Events.OnValidatePrincipal = AdminPolicy.ValidateStampAsync;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminPolicy.Name, policy => policy.RequireClaim(AdminPolicy.ClaimType, AdminPolicy.ClaimValue));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
// Tek satır istek logu: yöntem, yol (sorgu dizesi yok), durum, süre. Statik Log.Logger kullanılmadığından
// logger DI'dan verilir.
app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Arayüz tr-TR; ondalık okuma InvariantDecimalModelBinder ile noktalı kalır.
var turkish = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkish),
    SupportedCultures = [turkish],
    SupportedUICultures = [turkish]
});

app.UseStatusCodePagesWithReExecute("/hata/{0}");
app.UseHttpsRedirection();
app.UseResponseCompression();
// Statik varlıklar asp-append-version ile sürümlendiği için bir yıl değişmez sayılır.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl =
        new StringValues("public, max-age=31536000, immutable")
});
app.UseRouting();
app.UseRateLimiter();
app.UseOutputCache();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
app.Use(AdminPolicy.RequirePasswordChangeAsync);

// /health canlılık: süreç ayakta mı (konteyner sağlık denetimi); /health/ready trafiği karşılayabilir mi.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadinessCheck.Tag) })
    .AllowAnonymous();
app.MapControllerRoute(
    name: "admin-root",
    pattern: "admin",
    defaults: new { area = "Admin", controller = "Dashboard", action = "Index" });
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Yedek komutları geçişlerden önce: gece yedeği şemaya dokunmaz, geri yükleme bozuk şemada da çalışır.
if (BackupCommand.BackupFolderFrom(args) is { } backupFolder)
{
    var backup = await BackupCommand.BackupAsync(
        app.Configuration.GetConnectionString("Default")!,
        app.Services.GetRequiredService<IProductImageStorage>().UploadsPath,
        backupFolder,
        app.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
        app.Services.GetRequiredService<IPrivateFileStorage>().RootPath);
    Console.WriteLine($"Yedek: {backup.Database}, arşiv: {backup.Archive ?? "(uploads yok)"}, belgeler: {backup.DocumentsArchive ?? "(belge yok)"}, silinen: {backup.Removed.Count}.");
    return;
}

if (BackupCommand.RestoreFileFrom(args) is { } restoreFile)
{
    var restored = await BackupCommand.RestoreAsync(app.Configuration.GetConnectionString("Default")!, restoreFile);
    Console.WriteLine($"Geri yükleme: {restored.Database}, {restored.Products} ürün.");
    return;
}

await DatabaseMigrator.ApplyAsync(
    app.Environment,
    app.Services,
    app.Services.GetRequiredService<ILogger<Program>>());
await SeedFirstAdminAsync(app);
await SeedCatalogAsync(app);

// Yönetici kurtarma: geçici parolayı konsola yazıp çıkar, sunucu açılmaz.
if (AdminResetCommand.EmailFrom(args) is { } resetEmail)
{
    var temporary = await AdminResetCommand.RunAsync(app.Services, resetEmail);
    Console.WriteLine($"Geçici parola: {temporary}");
    Console.WriteLine("İlk girişte parola değiştirilmeden panel açılmaz; iki adımlı doğrulama kapatıldı, açık oturumlar düştü.");
    return;
}

// Tek seferlik ithal: ürünleri ve görselleri kurup çıkar, sunucu açılmaz.
if (ImportCommand.DirectoryFrom(args) is { } importDirectory)
{
    var repoRoot = RepoPath.Root(app.Environment.ContentRootPath);
    await ImportCommand.RunAsync(
        app.Services,
        RepoPath.Resolve(repoRoot, importDirectory),
        RepoPath.Resolve(repoRoot, ImportCommand.TableFrom(args)));
    return;
}

// Tek seferlik görsel yenileme: yüklenmiş görselleri yeniden üretip çıkar, sunucu açılmaz.
if (RefreshImagesCommand.Requested(args))
{
    var refreshRaw = RefreshImagesCommand.DirectoryFrom(args);
    await RefreshImagesCommand.RunAsync(
        app.Services,
        refreshRaw is null ? null : RepoPath.Resolve(RepoPath.Root(app.Environment.ContentRootPath), refreshRaw));
    return;
}

app.Run();

static async Task SeedFirstAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var email = app.Configuration["Admin:Email"];
    var password = app.Configuration["Admin:Password"];

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        logger.LogCritical("Admin:Email / Admin:Password tanımsız. İlk yönetici oluşturulmadı; /admin'e giriş yapılamaz.");
        return;
    }

    var authService = scope.ServiceProvider.GetRequiredService<IAdminAuthService>();
    var (_, result) = await authService.EnsureSeedAsync(email, password);
    logger.LogInformation("Yönetici tohumlama: {Message}", result.Message);
}

/// <summary>Katalog boşsa açılış ürünleri; yalnız Development'ta, Seed:Catalog=false ile kapatılır (testler).</summary>
static async Task SeedCatalogAsync(WebApplication app)
{
    if (!DataSeeder.ShouldSeed(app.Environment.EnvironmentName, app.Configuration.GetValue("Seed:Catalog", true)))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    await DataSeeder.SeedCatalogAsync(scope.ServiceProvider.GetRequiredService<HerYerdeContext>());
}

/// <summary>Yalnız yönetici çerezine sahip isteklerin /admin altına girmesini sağlar.</summary>
public static class AdminPolicy
{
    public const string Name = "Admin";
    public const string ClaimType = "heryerde:admin";
    public const string ClaimValue = "true";

    /// <summary>Çerezdeki parola damgası; veritabanındakiyle uymazsa oturum geçersizdir.</summary>
    public const string StampClaim = "heryerde:pwd";

    /// <summary>Geçici parolayla girildi: parola değişene kadar yalnız /admin/sifre açılır.</summary>
    public const string MustChangeClaim = "heryerde:must-change";

    /// <summary>Parola doğru, iki adımlı kod bekleniyor: değer çerezin kesildiği an (ticks). Bu çerezle panel açılmaz.</summary>
    public const string PendingSecondFactorClaim = "heryerde:2fa";

    public const string ChangePasswordPath = "/admin/sifre";

    public static Task RequirePasswordChangeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path;
        if (context.User.HasClaim(c => c.Type == MustChangeClaim)
            && path.StartsWithSegments("/admin")
            && !path.StartsWithSegments(ChangePasswordPath)
            && !path.StartsWithSegments("/admin/auth"))
        {
            // Çerez kimlik doğrulamasının giriş yönlendirmesi gibi mutlak adres.
            var request = context.Request;
            context.Response.Redirect($"{request.Scheme}://{request.Host}{request.PathBase}{ChangePasswordPath}");
            return Task.CompletedTask;
        }

        return next(context);
    }

    public static async Task ValidateStampAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal?.FindFirst(ClaimType) is null)
        {
            return;
        }

        var admin = int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        var stamp = long.TryParse(principal.FindFirst(StampClaim)?.Value, out var ticks) ? ticks : -1;
        var authService = context.HttpContext.RequestServices.GetRequiredService<IAdminAuthService>();

        if (admin == 0 || !await authService.StampIsCurrentAsync(admin, stamp, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

public partial class Program
{
}
