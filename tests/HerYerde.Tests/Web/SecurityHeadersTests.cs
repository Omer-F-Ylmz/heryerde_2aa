using System.Net;
using System.Text.RegularExpressions;

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
    }

    /// <summary>ZAP 10055: img-src'de "https:" gibi şema-joker kaynak yok; dış görsel yalnız Shop:ImageOrigins'ten.</summary>
    [Fact]
    public async Task Csp_img_src_sema_joker_kaynak_tasimaz()
    {
        var client = _factory.CreateNonRedirectingClient();

        var csp = Header(await client.GetAsync("/"), "Content-Security-Policy");

        var imgSrc = csp.Split(';', StringSplitOptions.TrimEntries).Single(d => d.StartsWith("img-src "));
        Assert.Equal("img-src 'self' data: https://placehold.co", imgSrc);
    }

    /// <summary>ZAP 90004: pencere ve kaynaklar başka kökenle paylaşılmaz.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/sepet")]
    [InlineData("/robots.txt")]
    public async Task Capraz_koken_yalitim_basliklari_her_yanitta_vardir(string url)
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync(url);

        Assert.Equal("same-origin", Header(response, "Cross-Origin-Opener-Policy"));
        Assert.Equal("same-origin", Header(response, "Cross-Origin-Resource-Policy"));
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

    /// <summary>ZAP 20012: token'sız form yalnız GET'tir ve data-no-csrf ile bilerek işaretlenmiştir.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/ara?q=tencere")]
    public async Task Tokensiz_form_yalniz_isaretli_get_formudur(string url)
    {
        var client = _factory.CreateNonRedirectingClient();

        var html = await (await client.GetAsync(url)).Content.ReadAsStringAsync();

        var forms = Regex.Matches(html, "<form[^>]*>.*?</form>", RegexOptions.Singleline);
        Assert.NotEmpty(forms);
        Assert.All(forms, form => Assert.True(
            form.Value.Contains("__RequestVerificationToken")
                || (form.Value.Contains("method=\"get\"") && form.Value.Contains("data-no-csrf")),
            form.Value[..Math.Min(120, form.Value.Length)]));
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
