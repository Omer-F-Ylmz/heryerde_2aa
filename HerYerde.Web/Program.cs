using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using HerYerde.Business.DependencyResolvers.Autofac;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Web.Infrastructure;
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
