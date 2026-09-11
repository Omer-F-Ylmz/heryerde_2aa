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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container => container.RegisterModule(new AutofacBusinessModule()));

// Konsol + 14 gün tutulan günlük dosya; telefon/e-posta/adres PiiMaskEnricher ile maskelenir.
// DI'daki ek sink'ler (testlerin bellek sink'i) ReadFrom.Services ile bağlanır; DI'a sonradan eklenen
// ILoggerProvider'lar (testlerin sorgu sayacı) writeToProviders ile beslenir. Varsayılan konsol sağlayıcısı
// Serilog konsoluyla çift yazmasın diye temizlenir.
builder.Logging.ClearProviders();
builder.Services.AddSerilog((services, logger) => logger
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.With<PiiMaskEnricher>()
    .WriteTo.Console()
    .WriteTo.File("logs/heryerde-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .ReadFrom.Services(services),
    preserveStaticLogger: true,
    writeToProviders: true);

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new InvariantDecimalModelBinderProvider());
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
builder.Services.AddHealthChecks();

// TLS'i sonlandıran vekil arkasında şema ve istemci IP'si başlıktan okunur.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
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
});

// Üretimde çerezler her zaman Secure; geliştirmede http ile çalışılabilsin diye istekle aynı.
builder.Services.Configure<CookiePolicyOptions>(options => options.Secure = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<CartCleanupHostedService>();

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
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
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

app.MapHealthChecks("/health");
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

await DatabaseMigrator.ApplyAsync(
    app.Environment,
    app.Services,
    app.Services.GetRequiredService<ILogger<Program>>());
await SeedFirstAdminAsync(app);
await SeedCatalogAsync(app);

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

/// <summary>Katalog boşsa açılış ürünleri; Seed:Catalog=false ile kapatılır (testler).</summary>
static async Task SeedCatalogAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("Seed:Catalog", true))
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
