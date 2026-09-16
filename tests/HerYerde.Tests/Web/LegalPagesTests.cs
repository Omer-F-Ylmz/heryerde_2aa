using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HerYerde.Business;

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
        // Tarih LegalDocs'tan gelir: metin her güncellendiğinde testi elle düzeltmek gerekmesin.
        Assert.Contains("Son güncelleme: " + LegalDocs.UpdatedAt.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR")), html);
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

    /// <summary>KAPANIŞ-2 V-KVKK: D7 kartla ödeme (İyzico), D6 e-posta bildirimi, D9 iletişim formu ve yorum metinlerde
    /// anlatılır; "kart bilgisi alınmaz" gibi artık yanlış cümle kalmaz.</summary>
    [Fact]
    public async Task Yasal_metinler_kart_odemesini_eposta_iletisim_ve_yorum_islemesini_anlatir()
    {
        var client = _factory.CreateNonRedirectingClient();
        async Task<string> Page(string slug) => await (await client.GetAsync("/yasal/" + slug)).Content.ReadAsStringAsync();

        var kvkk = await Page("kvkk-aydinlatma");
        var privacy = await Page("gizlilik-politikasi");
        var preInfo = await Page("on-bilgilendirme-formu");
        var contract = await Page("mesafeli-satis-sozlesmesi");

        Assert.All(new[] { kvkk, privacy, preInfo }, html =>
        {
            Assert.DoesNotContain("Kart bilgisi istemeyiz", html);
            Assert.DoesNotContain("Kart bilgisi internet üzerinden alınmaz", html);
        });
        Assert.All(new[] { kvkk, privacy, preInfo, contract }, html => Assert.Contains("İyzico", html));
        Assert.Contains("İletişim formu", kvkk);
        Assert.Contains("Ürün yorumu", kvkk);
        Assert.Contains("e-posta hizmet sağlayıcısı", kvkk);
        Assert.Contains("İletişim formu mesajları: 1 yıl", kvkk);
        Assert.Contains("E-posta bildirimleri: gönderimden sonra 30 gün", kvkk);
    }

    /// <summary>KAPANIŞ-2 V-ENV: D6-D9 tabloları, TempData çerezi ve temizlik işleri veri envanterinde yer alır.</summary>
    [Fact]
    public void Veri_envanteri_yeni_tablolari_cerezi_ve_temizlik_islerini_kapsar()
    {
        var inventory = RepoFile.ReadAllText("docs", "veri-envanteri.md");

        Assert.Contains("`outbox_message`", inventory);
        Assert.Contains("`payment`", inventory);
        Assert.Contains("`.AspNetCore.Mvc.CookieTempDataProvider`", inventory);
        Assert.Contains("PersonalDataCleanupHostedService", inventory);
        Assert.DoesNotContain("[MÜŞTERİ: saklama süresi]", inventory);
    }

    [Fact]
    public async Task Cerez_politikasi_yalniz_zorunlu_cerezleri_sayar_ve_ucuncu_taraf_olmadigini_soyler()
    {
        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/yasal/cerez-politikasi")).Content.ReadAsStringAsync();

        Assert.Contains("heryerde.cart", html);
        Assert.Contains(".AspNetCore.Antiforgery", html);
        Assert.Contains("heryerde.lastorder", html);
        Assert.Contains("heryerde.admin", html);
        // KAPANIŞ-2 V-CEREZ: yorum ve ödeme hatası bildirimleri TempData çereziyle taşınır.
        Assert.Contains(".AspNetCore.Mvc.CookieTempDataProvider", html);
        Assert.Contains("üçüncü taraf", html);
        Assert.Contains("onay bandı", html);
    }
}
