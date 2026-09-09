using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using HerYerde.Business.Abstract;
using HerYerde.Business.DependencyResolvers.Autofac;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container => container.RegisterModule(new AutofacBusinessModule()));

builder.Services.AddControllersWithViews(options =>
    options.ModelBinderProviders.Insert(0, new InvariantDecimalModelBinderProvider()));

// Türkçe harfler HTML kaynağında entity'ye çevrilmesin.
builder.Services.AddSingleton(HtmlEncoder.Create(
    UnicodeRanges.BasicLatin,
    UnicodeRanges.Latin1Supplement,
    UnicodeRanges.LatinExtendedA));

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddDbContext<HerYerdeContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHealthChecks();

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
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminPolicy.Name, policy => policy.RequireClaim(AdminPolicy.ClaimType, AdminPolicy.ClaimValue));

var app = builder.Build();

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
app.UseStaticFiles();
app.UseRouting();
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
}

public partial class Program
{
}
