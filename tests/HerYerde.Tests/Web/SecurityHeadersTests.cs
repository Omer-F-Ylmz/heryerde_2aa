using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>S07/S03/S06/S14: her yanıtta güvenlik başlıkları, global antiforgery, güvenli çerez, host süzgeci.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SecurityHeadersTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/sepet")]
    [InlineData("/admin/auth/login")]
    [InlineData("/yok-boyle-bir-sayfa")]
    public async Task Guvenlik_basliklari_her_yanitta_vardir(string url)
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync(url);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Header(response, "Permissions-Policy"));
        Assert.Contains("microphone=()", Header(response, "Permissions-Policy"));
        Assert.Contains("geolocation=()", Header(response, "Permissions-Policy"));

        var csp = Header(response, "Content-Security-Policy");
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("form-action 'self'", csp);
        Assert.Contains("img-src 'self' https: data:", csp);
    }

    [Fact]
    public async Task Gelistirmede_hsts_baslgi_yollanmaz()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync("/");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Tokensiz_post_global_antiforgery_ile_reddedilir()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.PostAsync("/sepet/ekle", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["productId"] = "1", ["quantity"] = "1" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Uretimde_cerezler_http_uzerinden_bile_secure_isaretlenir()
    {
        using var factory = new ProductionFactory();
        var client = factory.CreateNonRedirectingClient();
        await SeedProductAsync();

        var response = await client.GetAsync("/urun/cam-surahi");

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.Contains("Antiforgery"));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gelistirmede_http_cerezi_secure_isaretlenmez()
    {
        var client = _factory.CreateNonRedirectingClient();
        await SeedProductAsync();

        var response = await client.GetAsync("/urun/cam-surahi");

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.Contains("Antiforgery"));
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Uretimde_izinli_olmayan_host_basligi_400_alir()
    {
        using var factory = new ProductionFactory();
        var client = factory.CreateNonRedirectingClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "evil.example.com";
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Uretimde_izinli_host_basligi_gecer()
    {
        using var factory = new ProductionFactory();
        var client = factory.CreateNonRedirectingClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = ProductionFactory.Host;
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<int> SeedProductAsync()
    {
        await using var context = TestDb.NewContext();
        return await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
    }

    private static string Header(HttpResponseMessage response, string name)
        => Assert.Single(response.Headers.GetValues(name));
}
