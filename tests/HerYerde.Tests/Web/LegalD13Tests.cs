using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HerYerde.Business;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using HerYerde.Web.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;

namespace HerYerde.Tests.Web;

/// <summary>D13 A3-A4: sözleşme PDF'i (sipariş postası eki, teşekkür sayfası indirmesi, sürüm arşivi), cayma formu, garanti ve
/// tüketici hakları sayfası, KDV ibaresi, kargoya veriliş süresi, yasal metin punto tabanı, Search Console doğrulama etiketi.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class LegalD13Tests : IAsyncLifetime
{
    private readonly PrivateRootFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Siparis_alindi_postasi_onaylanan_surumun_sozlesme_pdfini_ekler_ve_dagitimda_gondericiye_iletir()
    {
        await using var context = TestDb.NewContext();
        var sender = new FakeNotificationSender();
        var order = await PlaceOrderAsync(context);

        await TestData.NewNotificationManager(context, sender).DispatchAsync();

        var customerMail = Assert.Single(await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.OrderPlaced));
        Assert.Equal(LegalDocs.ArchivePath(order.LegalVersion!), customerMail.Attachment);
        Assert.Equal("sozlesmeler/" + LegalDocs.Version + ".pdf", customerMail.Attachment);
        Assert.Null(Assert.Single(await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.NewOrderForStore)).Attachment);
        Assert.Contains(sender.Attachments, a => a == customerMail.Attachment);
    }

    [Fact]
    public async Task Sozlesme_pdfi_siparisteki_surumu_tasir_arsive_yazilir_arsivsiz_eski_surum_ve_yanlis_anahtar_404()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context);
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync($"/siparis/{order.OrderNo}/sozlesme?t={order.AccessToken}");
        var wrongToken = await client.GetAsync($"/siparis/{order.OrderNo}/sozlesme?t={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var text = PdfText(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Sürüm " + order.LegalVersion, text);
        Assert.Contains("Ön bilgilendirme formu", text);
        Assert.Contains("Mesafeli satış sözleşmesi", text);
        Assert.Contains("Cayma hakkı", text);
        // Sayfa düzeni (arama kutusu, altbilgi, içindekiler) PDF'e girmez; yalnız metin gövdesi.
        Assert.DoesNotContain("Ürün ara", text);
        Assert.DoesNotContain("İçindekiler", text);
        Assert.True(File.Exists(Path.Combine(_factory.PrivateRoot, "sozlesmeler", order.LegalVersion + ".pdf")));
        Assert.Equal(HttpStatusCode.NotFound, wrongToken.StatusCode);

        var tracked = (await new EfOrderDal(context).GetTrackedAsync(o => o.Id == order.Id))!;
        tracked.LegalVersion = "2020-01-01.1";
        await new EfUnitOfWork(context).SaveChangesAsync();
        var old = await client.GetAsync($"/siparis/{order.OrderNo}/sozlesme?t={order.AccessToken}");
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }

    [Fact]
    public async Task Tesekkur_sayfasi_sozlesme_pdfi_baglantisi_verir()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context);

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}"))
            .Content.ReadAsStringAsync();

        Assert.Contains($"href=\"/siparis/{order.OrderNo}/sozlesme?t={order.AccessToken}\"", html);
    }

    [Fact]
    public void Smtp_iletisi_eki_pdf_olarak_tasir()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".pdf");
        File.WriteAllBytes(path, "%PDF-1.7 deneme"u8.ToArray());

        var message = SmtpNotificationSender.BuildMessage("siparis@heryerde.test", "ayse@example.com", "Siparişiniz alındı · HY-1", "<p>x</p>", path, "sozlesme.pdf");

        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("sozlesme.pdf", attachment.ContentDisposition!.FileName);
        Assert.Equal("application/pdf", attachment.ContentType.MimeType);
        File.Delete(path);
    }

    [Fact]
    public async Task Cayma_formu_sayfasi_doldurulabilir_bos_ve_doldurulmus_pdf_uretir()
    {
        var client = _factory.CreateNonRedirectingClient();

        var page = await client.GetAsync("/yasal/cayma-formu");
        var blank = await client.GetAsync("/yasal/cayma-formu/pdf");
        var filled = await HtmlForm.PostAsync(client, "/yasal/cayma-formu", "/yasal/cayma-formu/pdf", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Address"] = "Cumhuriyet Mah. 12/3 Kadıköy/İstanbul",
            ["OrderNo"] = "HY-20260115-0001",
            ["OrderDate"] = "15.01.2026",
            ["Items"] = "Çelik Tencere × 1",
            ["Amount"] = "450,00 ₺"
        });

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("action=\"/yasal/cayma-formu/pdf\"", html);
        Assert.Contains("name=\"FullName\"", html);
        Assert.Equal("application/pdf", blank.Content.Headers.ContentType!.MediaType);
        Assert.Contains("CAYMA FORMU", PdfText(await blank.Content.ReadAsByteArrayAsync()));
        Assert.Equal(HttpStatusCode.OK, filled.StatusCode);
        var text = PdfText(await filled.Content.ReadAsByteArrayAsync());
        Assert.Contains("Ayşe Yılmaz", text);
        Assert.Contains("HY-20260115-0001", text);
    }

    [Fact]
    public async Task Garanti_ve_tuketici_haklari_sayfasi_200_sitemap_ve_altbilgide()
    {
        var client = _factory.CreateNonRedirectingClient();

        var page = await client.GetAsync("/yasal/garanti-ve-tuketici-haklari");
        var xml = await (await client.GetAsync("/sitemap.xml")).Content.ReadAsStringAsync();
        var home = await (await client.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("hakem heyeti", html);
        Assert.Contains("[MÜŞTERİ:", html);
        Assert.Contains("href=\"/yasal/cayma-formu\"", html);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = XDocument.Parse(xml).Root!.Elements(ns + "url").Select(u => (string)u.Element(ns + "loc")!).ToList();
        Assert.Contains($"{AdminWebFactory.BaseUrl}/yasal/garanti-ve-tuketici-haklari", locs);
        Assert.Contains($"{AdminWebFactory.BaseUrl}/yasal/cayma-formu", locs);
        Assert.Contains("href=\"/yasal/garanti-ve-tuketici-haklari\"", home);
    }

    [Fact]
    public async Task Kdv_ibaresi_urun_fiyat_blogunda_ve_altbilgide()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        }

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();

        var priceBlock = Regex.Match(html, "<p class=\"price-block price-block--lg\">.*?</p>", RegexOptions.Singleline);
        Assert.True(priceBlock.Success, "Ürün sayfasında fiyat bloğu yok.");
        Assert.Contains("Fiyatlara KDV dahildir", priceBlock.Value);
        var footer = Regex.Match(html, "<footer class=\"store-footer\">.*?</footer>", RegexOptions.Singleline);
        Assert.Contains("Fiyatlara KDV dahildir", footer.Value);
    }

    [Fact]
    public async Task Kargoya_verilis_suresi_ayardan_urun_sayfasinda_ve_sepette_yazar()
    {
        using var factory = new DispatchFactory();
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        }

        var client = factory.CreateNonRedirectingClient();
        var product = await (await client.GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });
        var cart = await (await client.GetAsync("/sepet")).Content.ReadAsStringAsync();

        Assert.Contains("4 iş günü içinde kargoda", product);
        Assert.Contains("4 iş günü içinde kargoda", cart);
    }

    [Fact]
    public void Yasal_metin_govdesi_en_az_12_punto()
    {
        var css = Regex.Replace(RepoFile.ReadAllText("src", "input.css"), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var body = Assert.Single(CheckoutCssTests.Rules(css, ".legal__body"));
        var size = Regex.Match(body, @"font-size:\s*(\d+)px;");
        Assert.True(size.Success, ".legal__body punto tanımlamıyor.");
        // 12 pt = 16 px (CSS'te 1 pt = 4/3 px).
        Assert.True(int.Parse(size.Groups[1].Value) >= 16);
    }

    [Fact]
    public async Task Search_console_dogrulama_etiketi_ayar_bosken_yok_doluyken_var()
    {
        using var verified = new VerificationFactory();

        var empty = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();
        var filled = await (await verified.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("google-site-verification", empty);
        Assert.Contains("<meta name=\"google-site-verification\" content=\"dogrulama-kodu-123\" />", filled);
    }

    [Fact]
    public async Task Aydinlatma_envanter_ve_imha_politikasi_iade_talebini_ve_sozlesme_arsivini_kapsar()
    {
        var kvkk = await (await _factory.CreateNonRedirectingClient().GetAsync("/yasal/kvkk-aydinlatma")).Content.ReadAsStringAsync();
        var inventory = RepoFile.ReadAllText("docs", "veri-envanteri.md");
        var policy = RepoFile.ReadAllText("docs", "veri-imha-politikasi.md");

        Assert.Contains("İade ve değişim talebi", kvkk);
        Assert.Contains("geri ödeme için IBAN", kvkk);
        Assert.Contains("`return_request`", inventory);
        Assert.Contains("/app/private/iadeler/", inventory);
        Assert.Contains("/app/private/sozlesmeler/", inventory);
        Assert.Contains("Periyodik imha", policy);
        Assert.Contains("6 ay", policy);
        Assert.Contains("`return_request`", policy);
        Assert.NotEqual("2026-09-15.2", LegalDocs.Version);
    }

    internal static string PdfText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join(" ", document.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
    }

    private static async Task<Order> PlaceOrderAsync(HerYerde.DataAccess.Concrete.EntityFramework.Contexts.HerYerdeContext context)
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);
        var (_, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", "05424970982", "ayse@example.com", "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, PaymentMethod.KapidaOdeme));
        return result.Data!;
    }

    private sealed class DispatchFactory : AdminWebFactory
    {
        protected override void Configure(Dictionary<string, string?> settings) => settings["Shop:DispatchDays"] = "4";
    }

    private sealed class VerificationFactory : AdminWebFactory
    {
        protected override void Configure(Dictionary<string, string?> settings) => settings["Seo:GoogleVerification"] = "dogrulama-kodu-123";
    }
}

/// <summary>Gizli belgeleri geçici klasöre yazan fabrika; sözleşme arşivi ve iade fotoğrafı testleri dosyayı buradan denetler.</summary>
public sealed class PrivateRootFactory : AdminWebFactory
{
    public string PrivateRoot { get; } = Path.Combine(Path.GetTempPath(), "heryerde-private-" + Guid.NewGuid().ToString("n"));

    protected override void Configure(Dictionary<string, string?> settings) => settings["PrivateFiles:Root"] = PrivateRoot;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(PrivateRoot))
        {
            Directory.Delete(PrivateRoot, recursive: true);
        }
    }
}
