using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D15 A2: çeyiz listesi. Sahip tokenli yönetim bağlantısıyla ürün seçer; paylaşılan adresten alınan hediye
/// ayrı sepet satırı olur, sipariş kesinleşince (havalede anında, kartta ödeme onayında) alınan adet artar ve sahibe
/// posta gider. Sahip kendi listesinden alamaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class GiftRegistryTests : IAsyncLifetime
{
    private const string BuyerPhone = "0542 497 09 82";

    private readonly CardPaymentFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Liste_olusturulunca_on_karakterlik_adres_ve_yonetim_postasi_uretilir()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await HtmlForm.PostAsync(client, "/ceyizlistesi/yeni", "/ceyizlistesi/yeni", new Dictionary<string, string>
        {
            ["OwnerName"] = "Zeynep Kaya",
            ["Phone"] = "0532 111 22 33",
            ["Email"] = "zeynep@ornek.test",
            ["EventDate"] = "2026-11-14",
            ["Message"] = "Yeni evimiz için."
        });

        await using var context = TestDb.NewContext();
        var registry = await context.GiftRegistries.SingleAsync();
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal($"/ceyizlistesi/yonet?t={registry.ManageToken}", response.Headers.Location?.OriginalString);
        Assert.Matches("^[a-z2-9]{10}$", registry.Slug);
        Assert.Equal("05321112233", registry.Phone);
        Assert.True(registry.IsPublic);

        var mail = Assert.Single(await context.OutboxMessages.Where(m => m.Type == OutboxType.GiftRegistryCreated).ToListAsync());
        Assert.Equal("zeynep@ornek.test", mail.To);
        Assert.Contains($"{TestData.BaseUrl}/ceyizlistesi/yonet?t={registry.ManageToken}", mail.Body);
        Assert.Contains($"{TestData.BaseUrl}/ceyizlistesi/{registry.Slug}", mail.Body);
    }

    [Fact]
    public async Task Yonetim_sayfasindan_urun_eklenir_adet_degisir_ve_silinir()
    {
        int productId;
        Guid token;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            (_, _, token) = await TestData.AddGiftRegistryAsync(context);
        }

        var client = _factory.CreateNonRedirectingClient();
        // Anahtar yolda değil sorguda: istek logu sorgu dizesini yazmaz.
        var manage = $"/ceyizlistesi/yonet?t={token}";

        var search = await (await client.GetAsync(manage + "&ara=tencere")).Content.ReadAsStringAsync();
        Assert.Contains("Çelik Tencere", search);
        // Sayfanın araması "ara"da: "q" olsaydı yerleşimdeki site arama kutusu da dolardı.
        Assert.Contains("id=\"registry-q\" name=\"ara\"", search);

        var added = await HtmlForm.PostAsync(client, manage, "/ceyizlistesi/yonet/ekle", new Dictionary<string, string>
        {
            ["t"] = token.ToString(),
            ["productId"] = productId.ToString(CultureInfo.InvariantCulture),
            ["desiredQty"] = "2"
        });
        Assert.Equal(HttpStatusCode.Found, added.StatusCode);

        int itemId;
        await using (var context = TestDb.NewContext())
        {
            var item = await context.GiftRegistryItems.SingleAsync();
            Assert.Equal((productId, 2, 0), (item.ProductId, item.DesiredQty, item.ReceivedQty));
            itemId = item.Id;
        }

        await HtmlForm.PostAsync(client, manage, "/ceyizlistesi/yonet/adet", new Dictionary<string, string>
        {
            ["t"] = token.ToString(),
            ["itemId"] = itemId.ToString(CultureInfo.InvariantCulture),
            ["desiredQty"] = "5"
        });
        await using (var context = TestDb.NewContext())
        {
            Assert.Equal(5, (await context.GiftRegistryItems.SingleAsync()).DesiredQty);
        }

        var removed = await HtmlForm.PostAsync(client, manage, "/ceyizlistesi/yonet/sil", new Dictionary<string, string>
        {
            ["t"] = token.ToString(),
            ["itemId"] = itemId.ToString(CultureInfo.InvariantCulture)
        });
        Assert.Equal(HttpStatusCode.Found, removed.StatusCode);
        await using (var context = TestDb.NewContext())
        {
            Assert.Empty(await context.GiftRegistryItems.ToListAsync());
        }
    }

    [Fact]
    public async Task Bilinmeyen_yonetim_anahtari_404_alir_ve_sayfa_onbelleklenmez()
    {
        Guid token;
        await using (var context = TestDb.NewContext())
        {
            (_, _, token) = await TestData.AddGiftRegistryAsync(context);
        }

        var client = _factory.CreateNonRedirectingClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/ceyizlistesi/yonet?t={Guid.NewGuid()}")).StatusCode);
        var page = await client.GetAsync($"/ceyizlistesi/yonet?t={token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("no-store", page.Headers.CacheControl?.ToString());
        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\"", await page.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Paylasilan_sayfa_kalan_adedi_gosterir_ve_indekslenmez()
    {
        string slug;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            int registryId;
            (registryId, slug, _) = await TestData.AddGiftRegistryAsync(context);
            await TestData.AddGiftRegistryItemAsync(context, registryId, productId, desired: 3, received: 1);
        }

        var response = await _factory.CreateNonRedirectingClient().GetAsync($"/ceyizlistesi/{slug}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\"", html);
        Assert.Contains("Zeynep Kaya", html);
        Assert.Contains("Çelik Tencere", html);
        Assert.Contains("1 / 3 alındı", html);
        Assert.Contains($"action=\"/ceyizlistesi/{slug}/hediye\"", html);
        // Sahibin iletişim bilgisi paylaşılan sayfada yer almaz.
        Assert.DoesNotContain("05321112233", html);
        Assert.DoesNotContain("zeynep@ornek.test", html);
    }

    [Fact]
    public async Task Gizli_liste_paylasilan_adreste_404_alir()
    {
        string slug;
        await using (var context = TestDb.NewContext())
        {
            (_, slug, _) = await TestData.AddGiftRegistryAsync(context, isPublic: false);
        }

        var response = await _factory.CreateNonRedirectingClient().GetAsync($"/ceyizlistesi/{slug}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Hediye_et_sepete_liste_satiri_ekler_kendi_alimiyla_birlesmez()
    {
        int productId;
        int itemId;
        string slug;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            int registryId;
            (registryId, slug, _) = await TestData.AddGiftRegistryAsync(context);
            itemId = await TestData.AddGiftRegistryItemAsync(context, registryId, productId);
        }

        var client = _factory.CreateNonRedirectingClient();
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(CultureInfo.InvariantCulture),
            ["quantity"] = "1"
        });
        var gifted = await GiftAsync(client, slug, itemId);

        Assert.Equal(HttpStatusCode.Found, gifted.StatusCode);
        await using var check = TestDb.NewContext();
        var lines = await check.CartItems.OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.Null(lines[0].GiftRegistryItemId);
        Assert.Equal(itemId, lines[1].GiftRegistryItemId);

        var cart = await (await client.GetAsync("/sepet")).Content.ReadAsStringAsync();
        Assert.Contains("Zeynep Kaya çeyiz listesi için", cart);
    }

    [Fact]
    public async Task Kalan_adetten_fazlasi_sepete_eklenemez()
    {
        int itemId;
        string slug;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            int registryId;
            (registryId, slug, _) = await TestData.AddGiftRegistryAsync(context);
            itemId = await TestData.AddGiftRegistryItemAsync(context, registryId, productId, desired: 2, received: 1);
        }

        var client = _factory.CreateNonRedirectingClient();
        Assert.Equal(HttpStatusCode.Found, (await GiftAsync(client, slug, itemId)).StatusCode);
        var second = await GiftAsync(client, slug, itemId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal(1, (await check.CartItems.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task Havale_siparisinde_alinan_adet_artar_ve_sahibine_posta_gider()
    {
        int itemId;
        string slug;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            int registryId;
            (registryId, slug, _) = await TestData.AddGiftRegistryAsync(context);
            itemId = await TestData.AddGiftRegistryItemAsync(context, registryId, productId);
        }

        var client = _factory.CreateNonRedirectingClient();
        await GiftAsync(client, slug, itemId);
        var placed = await CheckoutAsync(client, PaymentMethod.HavaleEft, BuyerPhone);

        Assert.Equal(HttpStatusCode.Found, placed.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal(1, (await check.GiftRegistryItems.SingleAsync()).ReceivedQty);
        Assert.Equal(itemId, (await check.OrderItems.SingleAsync()).GiftRegistryItemId);
        var mail = Assert.Single(await check.OutboxMessages.Where(m => m.Type == OutboxType.GiftRegistryPurchase).ToListAsync());
        Assert.Equal("zeynep@ornek.test", mail.To);
        Assert.Contains("Ayşe Yılmaz", mail.Body);
        Assert.Contains("Çelik Tencere", mail.Body);
    }

    [Fact]
    public async Task Kartli_sipariste_alinan_adet_odeme_onayinda_artar()
    {
        int itemId;
        string slug;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            int registryId;
            (registryId, slug, _) = await TestData.AddGiftRegistryAsync(context);
            itemId = await TestData.AddGiftRegistryItemAsync(context, registryId, productId);
        }

        var client = _factory.CreateNonRedirectingClient();
        await GiftAsync(client, slug, itemId);
        await CheckoutAsync(client, PaymentMethod.KrediKarti, BuyerPhone);

        Payment payment;
        await using (var context = TestDb.NewContext())
        {
            Assert.Equal(0, (await context.GiftRegistryItems.SingleAsync()).ReceivedQty);
            payment = await context.Payments.SingleAsync();
        }

        await _factory.CreateNonRedirectingClient().PostAsync("/odeme/3d-donus", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["status"] = "success",
            ["paymentId"] = payment.PaymentId ?? "pay-0",
            ["conversationId"] = payment.ConversationId,
            ["conversationData"] = "3ds-veri",
            ["mdStatus"] = "1",
            ["signature"] = "imza"
        }));

        await using var check = TestDb.NewContext();
        Assert.Equal(PaymentStatus.Basarili, (await check.Payments.SingleAsync()).Status);
        Assert.Equal(1, (await check.GiftRegistryItems.SingleAsync()).ReceivedQty);
        Assert.Single(await check.OutboxMessages.Where(m => m.Type == OutboxType.GiftRegistryPurchase).ToListAsync());
    }

    [Fact]
    public async Task Liste_sahibi_kendi_listesinden_alamaz()
    {
        int itemId;
        string slug;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            int registryId;
            (registryId, slug, _) = await TestData.AddGiftRegistryAsync(context, phone: "05424970982");
            itemId = await TestData.AddGiftRegistryItemAsync(context, registryId, productId);
        }

        var client = _factory.CreateNonRedirectingClient();
        await GiftAsync(client, slug, itemId);
        var placed = await CheckoutAsync(client, PaymentMethod.HavaleEft, BuyerPhone);

        Assert.Equal(HttpStatusCode.Conflict, placed.StatusCode);
        Assert.Contains("Kendi çeyiz listenizden hediye alamazsınız.", await placed.Content.ReadAsStringAsync());
        await using var check = TestDb.NewContext();
        Assert.Empty(await check.Orders.ToListAsync());
        Assert.Equal(0, (await check.GiftRegistryItems.SingleAsync()).ReceivedQty);
    }

    [Fact]
    public async Task Yonetim_ceyiz_listelerini_ve_ilerlemeyi_gosterir()
    {
        string slug;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            int registryId;
            (registryId, slug, _) = await TestData.AddGiftRegistryAsync(context);
            await TestData.AddGiftRegistryItemAsync(context, registryId, productId, desired: 4, received: 1);
        }

        var anonymous = await _factory.CreateNonRedirectingClient().GetAsync("/admin/ceyizlistesi");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);

        var admin = await _factory.CreateSignedInClientAsync();
        var html = await (await admin.GetAsync("/admin/ceyizlistesi")).Content.ReadAsStringAsync();

        Assert.Contains("Zeynep Kaya", html);
        Assert.Contains($"/ceyizlistesi/{slug}", html);
        Assert.Matches(new Regex("1\\s*/\\s*4"), html);
    }

    private static Task<HttpResponseMessage> GiftAsync(HttpClient client, string slug, int itemId)
        => HtmlForm.PostAsync(client, $"/ceyizlistesi/{slug}", $"/ceyizlistesi/{slug}/hediye", new Dictionary<string, string>
        {
            ["itemId"] = itemId.ToString(CultureInfo.InvariantCulture)
        });

    private static Task<HttpResponseMessage> CheckoutAsync(HttpClient client, PaymentMethod method, string phone)
        => HtmlForm.PostAsync(client, "/odeme", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = phone,
            ["Email"] = "ayse@ornek.test",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)method).ToString(CultureInfo.InvariantCulture),
            ["CardHolderName"] = "AYSE YILMAZ",
            ["CardNumber"] = "5528790000000008",
            ["CardExpireMonth"] = "12",
            ["CardExpireYear"] = "2030",
            ["CardCvc"] = "123",
            ["LegalConsent"] = "true"
        });
}
