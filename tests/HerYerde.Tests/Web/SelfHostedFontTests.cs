using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>Yazı tipleri kendi sunucumuzdan gelir: sayfa da CSP de Google'a bakmaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SelfHostedFontTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Ana_sayfa_google_fonts_istegi_yapmaz()
    {
        var client = _factory.CreateNonRedirectingClient();

        var html = await (await client.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("fonts.googleapis.com", html);
        Assert.DoesNotContain("fonts.gstatic.com", html);
    }

    [Fact]
    public async Task Icerik_guvenlik_ilkesi_google_kaynaklarini_saymaz()
    {
        var client = _factory.CreateNonRedirectingClient();

        var csp = (await client.GetAsync("/")).Headers.GetValues("Content-Security-Policy").Single();

        Assert.DoesNotContain("fonts.googleapis.com", csp);
        Assert.DoesNotContain("fonts.gstatic.com", csp);
        Assert.Contains("font-src 'self'", csp);
    }

    [Theory]
    [InlineData("/fonts/figtree-latin.woff2")]
    [InlineData("/fonts/figtree-latin-ext.woff2")]
    [InlineData("/fonts/lora-latin.woff2")]
    [InlineData("/fonts/lora-latin-ext.woff2")]
    public async Task Yazi_tipi_dosyalari_sunulur(string path)
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength > 0);
    }

    [Fact]
    public async Task Stil_dosyasi_yerel_yazi_tiplerini_swap_ile_tanimlar()
    {
        var client = _factory.CreateNonRedirectingClient();

        var css = await (await client.GetAsync("/css/site.css")).Content.ReadAsStringAsync();

        Assert.Contains("/fonts/figtree-latin.woff2", css);
        Assert.Contains("/fonts/lora-latin.woff2", css);
        Assert.Contains("font-display:swap", css.Replace(" ", string.Empty));
    }
}
