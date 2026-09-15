using HerYerde.Business;

namespace HerYerde.Tests.Web;

/// <summary>KAPANIŞ-3 §5: D10-D12 ile gelen veri işlemeleri (havale bildirimi ve dekont, fatura, WhatsApp/Instagram/telefon
/// siparişi, kartla iade, yönetim işlem kaydı, yedek) yasal metinlerde anlatılır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class LegalTextsD11Tests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>V-01, V-09, V-15: havale bildirimi, dekont ve fatura veri türü; faturanın saklama dayanağı; İyzico iadesi.</summary>
    [Fact]
    public async Task Aydinlatma_havale_bildirimi_dekont_fatura_ve_kart_iadesini_anlatir()
    {
        var kvkk = await PageAsync("kvkk-aydinlatma");

        Assert.Contains("Havale/EFT bildirimi", kvkk);
        Assert.Contains("dekont", kvkk);
        Assert.Contains("fatura belgesi", kvkk);
        Assert.Contains("TTK m. 82", kvkk);
        Assert.Contains("alınması ve iadesi", kvkk);
    }

    /// <summary>V-02: veriler yalnız site formlarıyla değil, WhatsApp/Instagram/telefon/mağaza kanallarından da toplanır.</summary>
    [Fact]
    public async Task Aydinlatma_ve_gizlilik_manuel_siparis_kanallarini_anlatir()
    {
        var kvkk = await PageAsync("kvkk-aydinlatma");
        var privacy = await PageAsync("gizlilik-politikasi");

        Assert.Contains("WhatsApp, Instagram, telefon ya da mağazada", kvkk);
        Assert.Contains("Meta Platforms", kvkk);
        Assert.Contains("WhatsApp, Instagram, telefon ya da mağazada", privacy);
    }

    /// <summary>V-03: uzaktan kanaldan verilen sipariş de mesafeli sözleşmedir; bilgilendirme ve teyit anlatılır.</summary>
    [Fact]
    public async Task Mesafeli_sozlesme_whatsapp_ve_telefon_siparisini_kapsar()
    {
        var contract = await PageAsync("mesafeli-satis-sozlesmesi");

        Assert.Contains("WhatsApp, Instagram ya da telefonla", contract);
        Assert.Contains("teyit", contract);
    }

    /// <summary>V-04: kartla ödemenin iadesi aynı karta yapılır (MSY m. 13/2); teslimat sayfası kartı ödeme yöntemi olarak sayar.</summary>
    [Fact]
    public async Task Iade_metinleri_karta_iadeyi_ve_kartla_odemeyi_anlatir()
    {
        var contract = await PageAsync("mesafeli-satis-sozlesmesi");
        var returns = await PageAsync("teslimat-ve-iade");

        Assert.Contains("aynı karta", contract);
        Assert.Contains("aynı karta", returns);
        Assert.Contains("Kartla ödeme", returns);
    }

    /// <summary>V-06, V-07: yönetim işlem kaydındaki eski/yeni değer ve 14 günlük yedek aydınlatmada.</summary>
    [Fact]
    public async Task Aydinlatma_islem_kaydi_ve_yedek_suresini_anlatir()
    {
        var kvkk = await PageAsync("kvkk-aydinlatma");

        Assert.Contains("eski ve yeni değer", kvkk);
        Assert.Contains("Yedekler: en çok 14 gün", kvkk);
    }

    /// <summary>V-13: yönetici oturum çerezi kodla aynı süre (12 saat, kayar).</summary>
    [Fact]
    public async Task Cerez_politikasi_yonetici_oturumunu_12_saat_yazar()
    {
        var cookies = await PageAsync("cerez-politikasi");

        Assert.Contains("12 saat", cookies);
        Assert.DoesNotContain("8 saat", cookies);
    }

    /// <summary>V-01 (form), V-14: havale bildirim formu aydınlatmaya bağlanır; metin değişikliği yeni sürüm olarak onaylanır.</summary>
    [Fact]
    public async Task Havale_bildirim_formu_aydinlatmaya_baglanir_ve_surum_ilerler()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", stock: 5);
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cart.AddAsync(cartId, productId, null, 1);
        var order = (await TestData.NewOrderManager(context).PlaceAsync(cartId, new HerYerde.Business.Dtos.OrderDraft(
            "Ayşe Yılmaz", "0542 497 09 82", null, "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null,
            HerYerde.Entities.Enums.PaymentMethod.HavaleEft))).Item2.Data!;

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync();
        var form = html[html.IndexOf("odeme-bildir", StringComparison.Ordinal)..];

        Assert.Contains("/yasal/kvkk-aydinlatma", form[..form.IndexOf("</form>", StringComparison.Ordinal)]);
        Assert.NotEqual("2026-09-15", LegalDocs.Version);
    }

    private async Task<string> PageAsync(string slug)
        => await (await _factory.CreateNonRedirectingClient().GetAsync("/yasal/" + slug)).Content.ReadAsStringAsync();
}
