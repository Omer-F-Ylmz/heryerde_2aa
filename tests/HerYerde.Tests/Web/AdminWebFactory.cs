using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HerYerde.Tests.Web;

public class AdminWebFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@heryerde.test";
    public const string AdminPassword = "HerYerde!Test1";
    public const string BaseUrl = "https://heryerde.test";

    protected virtual string Environment => "Development";

    /// <summary>Alt sınıflar üretim ayarı gibi farklılıkları buradan ekler.</summary>
    protected virtual void Configure(Dictionary<string, string?> settings)
    {
    }

    /// <summary>Alt sınıflar sahte servisleri buradan koyar.</summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = TestDb.ConnectionString,
            ["Admin:Email"] = AdminEmail,
            ["Admin:Password"] = AdminPassword,
            ["Seed:Catalog"] = "false",
            ["Shop:BaseUrl"] = BaseUrl,
            ["Shop:FreeShippingOver"] = "2500",
            ["Notifications:StoreTo"] = TestData.StoreEmail,
            ["Shipping:Carriers:0:Name"] = TestData.Carrier,
            ["Shipping:Carriers:0:TrackingUrl"] = TestData.TrackingUrlTemplate
        };
        Configure(settings);

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(TestClock.Fixed);
            // Analitik yazımı testte gerçek saatle 30 sn'de bir çalışıp başka testin veritabanına kayıt taşımasın: testler AnalyticsWriter'ı elle boşaltır.
            foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(HerYerde.Web.Infrastructure.AnalyticsHostedService)).ToList())
            {
                services.Remove(hosted);
            }

            ConfigureServices(services);
        });
    }

    public HttpClient CreateNonRedirectingClient()
        => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Giriş yapmış, çerezi taşıyan istemci.</summary>
    public async Task<HttpClient> CreateSignedInClientAsync()
    {
        var client = CreateNonRedirectingClient();
        var response = await HtmlForm.PostAsync(client, "/admin/auth/login", "/admin/auth/login", new Dictionary<string, string>
        {
            ["Email"] = AdminEmail,
            ["Password"] = AdminPassword
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return client;
    }
}

/// <summary>Üretim ortamı davranışı: Secure çerez ve gerçek alan adı süzgeci.</summary>
public sealed class ProductionFactory : AdminWebFactory
{
    public const string Host = "localhost";

    protected override string Environment => "Production";

    protected override void Configure(Dictionary<string, string?> settings)
        => settings["AllowedHosts"] = Host;
}

public static class HtmlForm
{
    private static readonly Regex TokenPattern = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
        RegexOptions.IgnoreCase);

    public static async Task<string> AntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = TokenPattern.Match(html);
        Assert.True(match.Success, $"{url} sayfasında antiforgery alanı bulunamadı.");
        return match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string formUrl,
        string postUrl,
        Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await AntiforgeryTokenAsync(client, formUrl);
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
    }
}
