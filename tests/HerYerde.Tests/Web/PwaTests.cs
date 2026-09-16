using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace HerYerde.Tests.Web;

/// <summary>D16 A2: service worker sürümü statik varlık özetinden gelir ve sepet/ödeme/yönetim/hesap yollarını önbelleklemez;
/// manifest bağımsız uygulama olarak kurulabilir; çevrimdışı sayfa markalı ve dizine girmez.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PwaTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Service_worker_surumu_statik_varlik_ozetinden_gelir_ozel_yollari_onbelleklemez()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/sw.js");
        var script = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoCache);

        var assets = File.ReadAllBytes(RepoFile.PathOf("HerYerde.Web", "wwwroot", "css", "site.css"))
            .Concat(File.ReadAllBytes(RepoFile.PathOf("HerYerde.Web", "wwwroot", "js", "site.js")))
            .ToArray();
        var version = Convert.ToHexStringLower(SHA256.HashData(assets))[..12];
        Assert.Contains($"const VERSION = \"{version}\";", script);

        foreach (var privatePath in new[] { "/sepet", "/odeme", "/admin", "/hesap", "/siparis", "/olay", "/ceyizlistesi/yonet" })
        {
            Assert.Contains($"\"{privatePath}\"", script);
        }

        Assert.Contains("\"/cevrimdisi\"", script);
        Assert.Contains("const PAGE_LIMIT = 20;", script);
    }

    [Fact]
    public async Task Manifest_bagimsiz_uygulama_olarak_kurulabilir_alanlari_tasir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/site.webmanifest");
        using var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = manifest.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("/", root.GetProperty("id").GetString());
        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("/", root.GetProperty("scope").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("name").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("short_name").GetString()));
        Assert.Equal("#A8442A", root.GetProperty("theme_color").GetString());
        Assert.Equal("#F6F1E8", root.GetProperty("background_color").GetString());
        var sizes = root.GetProperty("icons").EnumerateArray().Select(i => i.GetProperty("sizes").GetString()).ToList();
        Assert.Contains("192x192", sizes);
        Assert.Contains("512x512", sizes);
    }

    [Fact]
    public async Task Cevrimdisi_sayfasi_markali_ve_dizine_girmez()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/cevrimdisi");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<meta name=\"robots\" content=\"noindex", html);
        Assert.Contains("class=\"store-header", html);
        Assert.Contains("Şu an çevrimdışısınız", html);
    }
}
