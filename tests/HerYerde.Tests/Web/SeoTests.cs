using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HerYerde.DataAccess.Concrete.EntityFramework;

namespace HerYerde.Tests.Web;

/// <summary>G01/G02/G03: sayfa başına meta, OG/Twitter kartı, canonical, sitemap ve robots.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SeoTests : IAsyncLifetime
{
    private const string Base = AdminWebFactory.BaseUrl;

    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Urun_sayfasi_title_description_ve_og_image_tasir()
    {
        var description = string.Concat(Enumerable.Repeat("Borosilikat camdan, ağzı geniş sürahi. ", 6));
        await using var context = TestDb.NewContext();
        var id = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", price: 1290.50m);
        await TestData.SetDescriptionAsync(context, id, description);
        await TestData.AddImageAsync(context, id, "/img/cam-surahi.jpg");

        var html = await GetHtmlAsync("/urun/cam-surahi");

        Assert.Contains("<title>Cam Sürahi · HerYerde</title>", html);
        var meta = PageHtml.Meta(html, "name", "description")!;
        Assert.StartsWith("Cam Sürahi · 1.290,50 ₺ · ", meta);
        Assert.Contains(description[..150].TrimEnd(), meta);
        Assert.DoesNotContain(description[..160], meta);
        Assert.Equal("Cam Sürahi", PageHtml.Meta(html, "property", "og:title"));
        Assert.Equal(meta, PageHtml.Meta(html, "property", "og:description"));
        Assert.Equal(Base + "/img/cam-surahi.jpg", PageHtml.Meta(html, "property", "og:image"));
        Assert.Equal(Base + "/urun/cam-surahi", PageHtml.Meta(html, "property", "og:url"));
        Assert.Equal("product", PageHtml.Meta(html, "property", "og:type"));
        Assert.Equal("summary_large_image", PageHtml.Meta(html, "name", "twitter:card"));
        Assert.Equal(Base + "/urun/cam-surahi", PageHtml.Canonical(html));
        Assert.Null(PageHtml.Meta(html, "name", "robots"));
    }

    [Fact]
    public async Task Kategori_canonical_query_stringsiz_ve_description_urun_sayisini_verir()
    {
        await using var context = TestDb.NewContext();
        var child = await TestData.AddChildCategoryAsync(context, "Mutfak & Sofra", "mutfak-sofra");
        await TestData.AddProductAsync(context, child, "Cam Sürahi", "cam-surahi");
        await TestData.AddProductAsync(context, child, "Çelik Tencere", "celik-tencere");

        var html = await GetHtmlAsync("/ev/mutfak-sofra?sirala=fiyat&min=10");

        Assert.Equal(Base + "/ev/mutfak-sofra", PageHtml.Canonical(html));
        var meta = PageHtml.Meta(html, "name", "description")!;
        Assert.Contains("Mutfak & Sofra", meta);
        Assert.Contains("2 ürün", meta);
        Assert.Equal("website", PageHtml.Meta(html, "property", "og:type"));
    }

    [Fact]
    public async Task Kategorinin_ikinci_sayfasi_canonical_olarak_kendini_gosterir()
    {
        await using var context = TestDb.NewContext();
        var child = await TestData.AddChildCategoryAsync(context, "Mutfak & Sofra", "mutfak-sofra");
        context.Products.AddRange(Enumerable.Range(1, 25).Select(i => TestData.NewProduct(child, $"Kase {i}", $"kase-{i}")));
        await context.SaveChangesAsync();

        var html = await GetHtmlAsync("/ev/mutfak-sofra?sirala=fiyat&sayfa=2");

        Assert.Equal(Base + "/ev/mutfak-sofra?sayfa=2", PageHtml.Canonical(html));
    }

    [Fact]
    public async Task Arama_sayfasi_noindex_isaretlenir()
    {
        var html = await GetHtmlAsync("/ara?q=cam");

        Assert.Equal("noindex, follow", PageHtml.Meta(html, "name", "robots"));
    }

    [Fact]
    public async Task Sitemap_aktif_urunleri_listeler_pasifleri_listelemez()
    {
        await using var context = TestDb.NewContext();
        var child = await TestData.AddChildCategoryAsync(context, "Mutfak & Sofra", "mutfak-sofra");
        await TestData.AddProductAsync(context, child, "Cam Sürahi", "cam-surahi");
        var passive = TestData.NewProduct(child, "Eski Sürahi", "eski-surahi");
        passive.IsActive = false;
        await new EfProductDal(context).AddAsync(passive);
        await new EfUnitOfWork(context).SaveChangesAsync();

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/sitemap.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urls = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!.Elements(ns + "url").ToList();
        var locs = urls.Select(u => (string)u.Element(ns + "loc")!).ToList();
        Assert.Contains(Base + "/", locs);
        Assert.Contains(Base + "/ev", locs);
        Assert.Contains(Base + "/ev/mutfak-sofra", locs);
        Assert.Contains(Base + "/urun/cam-surahi", locs);
        Assert.DoesNotContain(Base + "/urun/eski-surahi", locs);
        Assert.All(urls, u => Assert.NotNull(u.Element(ns + "lastmod")));
    }

    [Fact]
    public async Task Robots_ozel_alanlari_kapatir_ve_sitemapi_gosterir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        var lines = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.TrimEntries);
        foreach (var path in new[] { "/admin", "/sepet", "/odeme", "/siparis", "/ara" })
        {
            Assert.Contains($"Disallow: {path}", lines);
        }

        Assert.Contains($"Sitemap: {Base}/sitemap.xml", lines);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}

/// <summary>Sayfa başındaki SEO etiketlerini ve JSON-LD bloklarını HTML'den okur.</summary>
internal static class PageHtml
{
    public static string? Meta(string html, string attribute, string key)
    {
        var match = Regex.Match(html, $"<meta {attribute}=\"{Regex.Escape(key)}\" content=\"([^\"]*)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    public static string? Canonical(string html)
    {
        var match = Regex.Match(html, "<link rel=\"canonical\" href=\"([^\"]*)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    public static List<JsonElement> JsonLd(string html)
        => Regex.Matches(html, "<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Singleline)
            .Select(m => JsonDocument.Parse(m.Groups[1].Value).RootElement.Clone())
            .ToList();

    public static JsonElement Single(IEnumerable<JsonElement> blocks, string type)
        => Assert.Single(blocks, b => b.GetProperty("@type").GetString() == type);
}
