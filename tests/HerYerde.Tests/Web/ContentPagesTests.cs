using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Xml.Linq;
using HerYerde.Web.Models;

namespace HerYerde.Tests.Web;

/// <summary>D9: Hakkımızda · İletişim · SSS; altbilgi Kurumsal sütunu, sitemap, SSS markdown kaynağı ve FAQPage JSON-LD.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ContentPagesTests : IAsyncLifetime
{
    private static readonly string[] Paths = ["/hakkimizda", "/iletisim", "/sss"];

    private readonly AdminWebFactory _factory = new();

    public static IEnumerable<object[]> PathData => Paths.Select(path => new object[] { path });

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [MemberData(nameof(PathData))]
    public async Task Kurumsal_sayfa_200_doner_tek_h1_ve_canonical_tasir(string path)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Single(Regex.Matches(html, "<h1[ >]"));
        Assert.Contains($"<link rel=\"canonical\" href=\"{AdminWebFactory.BaseUrl}{path}\"", html);
    }

    [Fact]
    public async Task Sitemap_uc_kurumsal_sayfayi_listeler_robots_engellemez()
    {
        var client = _factory.CreateNonRedirectingClient();
        var xml = await (await client.GetAsync("/sitemap.xml")).Content.ReadAsStringAsync();
        var robots = await (await client.GetAsync("/robots.txt")).Content.ReadAsStringAsync();

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = XDocument.Parse(xml).Root!.Elements(ns + "url").Select(u => (string)u.Element(ns + "loc")!).ToList();
        Assert.All(Paths, path => Assert.Contains(AdminWebFactory.BaseUrl + path, locs));
        Assert.All(Paths, path => Assert.DoesNotContain("Disallow: " + path, robots));
    }

    [Fact]
    public async Task Altbilgideki_kurumsal_sutunu_uc_sayfaya_baglanir()
    {
        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        var nav = Regex.Match(html, "<nav class=\"store-footer__corporate\" aria-label=\"Kurumsal\">.*?</nav>", RegexOptions.Singleline);
        Assert.True(nav.Success, "Altbilgide Kurumsal sütunu yok.");
        Assert.All(Paths, path => Assert.Contains($"href=\"{path}\"", nav.Value));
    }

    [Fact]
    public async Task Hakkimizda_WhatsApp_CTA_si_ve_musteri_yer_tutuculari_tasir()
    {
        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/hakkimizda")).Content.ReadAsStringAsync();

        Assert.Contains("href=\"https://wa.me/905424970982", html);
        Assert.Contains("WhatsApp'tan yaz", html);
        Assert.Contains("[MÜŞTERİ: kuruluş yılı]", html);
        Assert.Contains("[MÜŞTERİ: sipariş sayısı]", html);
        Assert.Contains("[MÜŞTERİ: takipçi sayısı]", html);
    }

    [Fact]
    public async Task Iletisim_kanallari_ve_form_alanlari_gorunur()
    {
        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/iletisim")).Content.ReadAsStringAsync();

        Assert.Contains("href=\"https://wa.me/905424970982", html);
        Assert.Contains(Seo.InstagramUrl, html);
        Assert.Contains("[MÜŞTERİ: e-posta adresi]", html);
        Assert.Contains("[MÜŞTERİ: çalışma saatleri]", html);
        Assert.Contains("[MÜŞTERİ: açık adres]", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("name=\"Website\"", html);
        Assert.All(["Sipariş", "Ürün", "İade", "Diğer"], subject => Assert.Contains($">{subject}</option>", html));
    }

    [Fact]
    public void Sss_markdown_tablosu_kategori_soru_cevap_olarak_okunur()
    {
        const string markdown = """
            # SSS

            Açıklama paragrafı tabloya karışmaz.

            | Kategori | Soru | Cevap |
            |---|---|---|
            | Sipariş & Ödeme | Kapıda ödeme var mı? | Evet, kapıda nakit ya da kart. |
            | Kargo & Teslimat | Kargo ücreti ne kadar? | Sepette gösterilir \| eşik üstü bedava. |
            | Sipariş & Ödeme | Havale yapabilir miyim? | Evet. |
            """;

        var groups = FaqSource.Parse(markdown);

        Assert.Equal(["Sipariş & Ödeme", "Kargo & Teslimat"], groups.Select(g => g.Category));
        Assert.Equal(["Kapıda ödeme var mı?", "Havale yapabilir miyim?"], groups[0].Items.Select(i => i.Question));
        Assert.Equal("Sepette gösterilir | eşik üstü bedava.", groups[1].Items[0].Answer);
    }

    [Fact]
    public async Task Sss_sayfasi_docs_sss_md_deki_her_soruyu_dort_kategoride_gosterir()
    {
        var groups = FaqSource.Parse(RepoFile.ReadAllText("docs", "sss.md"));

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/sss")).Content.ReadAsStringAsync();

        Assert.Equal(["Sipariş & Ödeme", "Kargo & Teslimat", "İade & Değişim", "Ürünler"], groups.Select(g => g.Category));
        var questions = groups.SelectMany(g => g.Items).ToList();
        // Uygulamanın kodlayıcısı: Türkçe harfler olduğu gibi, HTML'e duyarlı karakterler kaçışlı.
        var encoder = HtmlEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA);
        Assert.All(questions, item => Assert.Contains(encoder.Encode(item.Question), html));
        Assert.Equal(questions.Count, Regex.Matches(html, "<details class=\"faq__item\"").Count);
        Assert.Equal(questions.Count, Regex.Matches(html, "<summary class=\"faq__question\"").Count);
    }

    [Fact]
    public async Task Sss_sayfasi_gecerli_FAQPage_JSON_LD_tasir()
    {
        var questions = FaqSource.Parse(RepoFile.ReadAllText("docs", "sss.md")).SelectMany(g => g.Items).ToList();

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/sss")).Content.ReadAsStringAsync();

        var faq = PageHtml.Single(PageHtml.JsonLd(html), "FAQPage");
        Assert.Equal("https://schema.org", faq.GetProperty("@context").GetString());
        var entities = faq.GetProperty("mainEntity").EnumerateArray().ToList();
        Assert.Equal(questions.Count, entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            Assert.Equal("Question", entities[i].GetProperty("@type").GetString());
            Assert.Equal(questions[i].Question, entities[i].GetProperty("name").GetString());
            var answer = entities[i].GetProperty("acceptedAnswer");
            Assert.Equal("Answer", answer.GetProperty("@type").GetString());
            Assert.Equal(questions[i].Answer, answer.GetProperty("text").GetString());
        }
    }

    [Fact]
    public async Task Urun_sayfasi_SSS_ye_baglanir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/urun/cam-surahi")).Content.ReadAsStringAsync();

        Assert.Contains("href=\"/sss\"", html);
    }
}
