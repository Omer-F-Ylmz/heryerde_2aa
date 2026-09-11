using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HerYerde.Tests.Web;

/// <summary>KAPANIŞ-5: altı yasal sayfa statik ve markalı; içindekiler, son güncelleme tarihi, sitemap ve altbilgi.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class LegalPagesTests : IAsyncLifetime
{
    private static readonly string[] AllSlugs =
    [
        "kvkk-aydinlatma",
        "gizlilik-politikasi",
        "cerez-politikasi",
        "mesafeli-satis-sozlesmesi",
        "on-bilgilendirme-formu",
        "teslimat-ve-iade"
    ];

    private readonly AdminWebFactory _factory = new();

    public static IEnumerable<object[]> Slugs => AllSlugs.Select(slug => new object[] { slug });

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [MemberData(nameof(Slugs))]
    public async Task Yasal_sayfa_indekslenir_icindekiler_ve_guncelleme_tarihi_tasir(string slug)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/yasal/" + slug);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("noindex", html);
        Assert.Single(Regex.Matches(html, "<h1[ >]"));
        Assert.Contains("Son güncelleme: 11 Eylül 2026", html);
        Assert.Contains($"<link rel=\"canonical\" href=\"{AdminWebFactory.BaseUrl}/yasal/{slug}\"", html);

        var toc = Regex.Matches(html, "<a class=\"legal__toc-link\" href=\"#(?<id>[a-z0-9-]+)\"")
            .Select(m => m.Groups["id"].Value)
            .ToList();
        Assert.True(toc.Count >= 3, "İçindekiler en az üç bölüm göstermeli.");
        Assert.All(toc, id => Assert.Contains($"id=\"{id}\"", html));
    }

    [Fact]
    public async Task Bilinmeyen_yasal_sayfa_404_doner()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/yasal/yok-boyle-bir-metin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sitemap_alti_yasal_sayfayi_listeler()
    {
        var xml = await (await _factory.CreateNonRedirectingClient().GetAsync("/sitemap.xml")).Content.ReadAsStringAsync();

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = XDocument.Parse(xml).Root!.Elements(ns + "url").Select(u => (string)u.Element(ns + "loc")!).ToList();
        Assert.All(AllSlugs, slug => Assert.Contains($"{AdminWebFactory.BaseUrl}/yasal/{slug}", locs));
    }

    [Fact]
    public async Task Altbilgideki_yasal_sutunu_alti_sayfaya_baglanir()
    {
        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        var nav = Regex.Match(html, "<nav class=\"store-footer__legal\" aria-label=\"Yasal\">.*?</nav>", RegexOptions.Singleline);
        Assert.True(nav.Success, "Altbilgide Yasal sütunu yok.");
        Assert.All(AllSlugs, slug => Assert.Contains($"href=\"/yasal/{slug}\"", nav.Value));
    }

    [Fact]
    public async Task Cerez_politikasi_yalniz_zorunlu_cerezleri_sayar_ve_ucuncu_taraf_olmadigini_soyler()
    {
        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/yasal/cerez-politikasi")).Content.ReadAsStringAsync();

        Assert.Contains("heryerde.cart", html);
        Assert.Contains(".AspNetCore.Antiforgery", html);
        Assert.Contains("heryerde.lastorder", html);
        Assert.Contains("heryerde.admin", html);
        Assert.Contains("üçüncü taraf", html);
        Assert.Contains("onay bandı", html);
    }
}
