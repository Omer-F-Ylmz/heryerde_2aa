using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HerYerde.Tests.Web;

/// <summary>Marka simgesi: favicon, dokunmatik simge, web manifest ve OG görseli olmayan sayfanın varsayılan kartı.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class BrandIconTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Favicon_ico_200_ve_16_32_48_boyutlarini_tasir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/favicon.ico");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        // ICONDIR: 0,1 (simge), kayıt sayısı; her kayıtta genişlik baytı (0 = 256).
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));
        var count = BitConverter.ToUInt16(bytes, 4);
        var widths = Enumerable.Range(0, count).Select(i => (int)bytes[6 + i * 16]).Order().ToArray();
        Assert.Equal([16, 32, 48], widths);
    }

    [Fact]
    public async Task Site_webmanifest_200_ve_gecerli_json()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/site.webmanifest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = manifest.RootElement;
        Assert.Equal("HerYerde", root.GetProperty("name").GetString());
        Assert.Equal("HerYerde", root.GetProperty("short_name").GetString());
        Assert.Equal("#A8442A", root.GetProperty("theme_color").GetString());
        Assert.Equal("#F6F1E8", root.GetProperty("background_color").GetString());
        var icons = root.GetProperty("icons").EnumerateArray().Select(i => i.GetProperty("src").GetString()).ToList();
        Assert.Contains("/icon-192.png", icons);
        Assert.Contains("/icon-512.png", icons);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/admin/auth/login")]
    public async Task Layout_icon_apple_touch_ve_manifest_baglantilarini_tasir(string url)
    {
        var html = await (await _factory.CreateNonRedirectingClient().GetAsync(url)).Content.ReadAsStringAsync();

        Assert.Contains("<link rel=\"icon\" href=\"/favicon.ico\" sizes=\"48x48\" />", html);
        Assert.Contains("<link rel=\"icon\" href=\"/favicon.svg\" type=\"image/svg+xml\" />", html);
        Assert.Contains("<link rel=\"apple-touch-icon\" href=\"/apple-touch-icon.png\" />", html);
        Assert.Contains("<link rel=\"manifest\" href=\"/site.webmanifest\" />", html);
        Assert.Contains("<meta name=\"theme-color\" content=\"#A8442A\" />", html);
    }

    [Fact]
    public async Task Og_gorseli_olmayan_sayfa_og_default_kullanir()
    {
        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/sss")).Content.ReadAsStringAsync();

        Assert.Equal(AdminWebFactory.BaseUrl + "/og-default.png", PageHtml.Meta(html, "property", "og:image"));
        Assert.Equal("summary_large_image", PageHtml.Meta(html, "name", "twitter:card"));
    }

    [Theory]
    [InlineData("/apple-touch-icon.png", 180, 180)]
    [InlineData("/icon-192.png", 192, 192)]
    [InlineData("/icon-512.png", 512, 512)]
    [InlineData("/og-default.png", 1200, 630)]
    public async Task Png_simgeler_beklenen_boyuttadir(string url, int width, int height)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        // PNG IHDR: genişlik 16-19, yükseklik 20-23 (big-endian).
        Assert.Equal(width, (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]);
        Assert.Equal(height, (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
    }

    [Fact]
    public async Task Favicon_svg_gradientsiz_ve_marka_paletinde()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/favicon.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Gradient", svg);
        var colors = Regex.Matches(svg, "#[0-9A-Fa-f]{6}").Select(m => m.Value.ToUpperInvariant()).Distinct();
        Assert.All(colors, c => Assert.Contains(c, new[] { "#A8442A", "#F6F1E8", "#0F6E5C" }));
    }
}
