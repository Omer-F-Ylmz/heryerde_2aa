using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HerYerde.Tests.Web;

/// <summary>G05: üretimde beklenmeyen hata markalı 500 sayfasına düşer; JSON isteyen ProblemDetails alır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ErrorPageTests : IAsyncLifetime
{
    private readonly FailingProductionFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Uretimde_beklenmeyen_hata_markali_500_sayfasi_doner()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FailingProductionFactory.FailingPath);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");

        var response = await _factory.CreateNonRedirectingClient().SendAsync(request);

        Assert.Equal(500, (int)response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Bir şeyler ters gitti", html);
        Assert.Contains("wa.me", html);
        Assert.Contains("class=\"wordmark\"", html);
        Assert.DoesNotContain(FailingProductionFactory.Message, html);
    }

    /// <summary>ZAP 10038/10063: hata sayfası yanıtı da güvenlik başlıklarını taşır.</summary>
    [Fact]
    public async Task Uretimde_500_yaniti_guvenlik_basliklarini_tasir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(FailingProductionFactory.FailingPath);

        Assert.Equal(500, (int)response.StatusCode);
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("camera=()", response.Headers.GetValues("Permissions-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Json_isteyen_istemci_ProblemDetails_alir()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FailingProductionFactory.FailingPath);
        request.Headers.Accept.ParseAdd("application/json");

        var response = await _factory.CreateNonRedirectingClient().SendAsync(request);

        Assert.Equal(500, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain(FailingProductionFactory.Message, body);
    }
}

/// <summary>Üretim ortamı + yalnız testte açılan, her istekte hata fırlatan bir uç.</summary>
public sealed class FailingProductionFactory : AdminWebFactory
{
    public const string FailingPath = "/test/patla";
    public const string Message = "Test amaçlı beklenmeyen hata.";

    protected override string Environment => "Production";

    protected override void Configure(Dictionary<string, string?> settings)
        => settings["AllowedHosts"] = ProductionFactory.Host;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, FailingEndpoint>());
    }

    /// <summary>Uygulamanın tüm ara katmanlarının arkasına eklenir; fırlayan hata onların içinden geçer.</summary>
    private sealed class FailingEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(FailingPath, branch => branch.Run(_ => throw new InvalidOperationException(Message)));
        };
    }
}
