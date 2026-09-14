using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace HerYerde.Tests.Web;

/// <summary>D7: İyzico 3D Secure ile kartla ödeme. Sağlayıcı sahte; ağ çağrısı yok.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CardPaymentTests : IAsyncLifetime
{
    private const string CardNumber = "5528790000000008";
    private const string Email = "ayse@ornek.test";
    private const string Callback = "/odeme/3d-donus";

    private readonly CardPaymentFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Anahtar_yokken_kart_secenegi_odeme_sayfasinda_gorunmez()
    {
        using var factory = new NoCardKeyFactory();
        await using var context = TestDb.NewContext();
        var client = factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);

        var html = await (await client.GetAsync("/odeme")).Content.ReadAsStringAsync();

        Assert.Contains("pay-kapida", html);
        Assert.DoesNotContain($"value=\"{(int)PaymentMethod.KrediKarti}\"", html);
        Assert.DoesNotContain("pay-kart", html);
    }

    [Fact]
    public async Task Anahtar_yokken_kartla_siparis_postu_400_alir()
    {
        using var factory = new NoCardKeyFactory();
        await using var context = TestDb.NewContext();
        var client = factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);

        var response = await PostCheckoutAsync(client, PaymentMethod.KrediKarti);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await new EfOrderDal(context).GetListAsync());
    }

    [Fact]
    public async Task Kart_siparisi_beklemede_ve_odeme_baslatildi_acilir_stok_dusmez()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);

        var response = await PostCheckoutAsync(client, PaymentMethod.KrediKarti);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"action=\"{FakePaymentProvider.FormAction}\"", html);
        var order = Assert.Single(await new EfOrderDal(context).GetListAsync());
        Assert.Equal(OrderStatus.Beklemede, order.Status);
        Assert.Equal(PaymentMethod.KrediKarti, order.PaymentMethod);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());
        Assert.Equal(PaymentStatus.Baslatildi, payment.Status);
        Assert.Equal(order.Id, payment.OrderId);
        Assert.Equal(order.Total, payment.Amount);
        Assert.Equal("iyzico", payment.Provider);
        Assert.Equal("pay-1", payment.PaymentId);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
        Assert.NotEmpty(await new EfCartItemDal(context).GetListAsync());
        Assert.DoesNotContain(await Outbox(context), m => m.Type == OutboxType.OrderPlaced);
    }

    [Fact]
    public async Task Basarili_donus_stok_dusurur_odemeyi_basarili_yapar_ve_musteri_mailini_kuyruga_koyar()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());

        // Dönüş sağlayıcının sayfasından gelir: çerez ve antiforgery anahtarı taşımaz.
        var response = await PostCallbackAsync(_factory.CreateNonRedirectingClient(), payment);

        var order = Assert.Single(await new EfOrderDal(context).GetListAsync());
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}", response.Headers.Location?.OriginalString);
        Assert.Equal(4, await TestData.ProductStockAsync(context, productId));
        Assert.Equal(PaymentStatus.Basarili, (await new EfPaymentDal(context).GetAsync(p => p.Id == payment.Id))!.Status);
        Assert.Equal(OrderStatus.Beklemede, order.Status);
        Assert.Contains(await Outbox(context), m => m.Type == OutboxType.OrderPlaced && m.To == Email);
        Assert.Contains(await Outbox(context), m => m.Type == OutboxType.NewOrderForStore);
        Assert.Empty(await new EfCartItemDal(context).GetListAsync());
    }

    [Fact]
    public async Task Basarisiz_donus_siparisi_iptal_eder_stok_degismez_ve_odeme_sayfasinda_mesaj_cikar()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());

        var response = await PostCallbackAsync(client, payment, status: "failure", mdStatus: "0");

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("/odeme", response.Headers.Location?.OriginalString);
        Assert.Equal(OrderStatus.IptalEdildi, Assert.Single(await new EfOrderDal(context).GetListAsync()).Status);
        Assert.Equal(PaymentStatus.Basarisiz, (await new EfPaymentDal(context).GetAsync(p => p.Id == payment.Id))!.Status);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
        Assert.DoesNotContain(await Outbox(context), m => m.Type == OutboxType.OrderPlaced);

        var page = await client.GetAsync("/odeme");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Ödemeniz onaylanmadı", await page.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ayni_donus_iki_kez_gelirse_stok_bir_kez_duser()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());

        var first = await PostCallbackAsync(_factory.CreateNonRedirectingClient(), payment);
        var second = await PostCallbackAsync(_factory.CreateNonRedirectingClient(), payment);

        Assert.Equal(HttpStatusCode.SeeOther, second.StatusCode);
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(4, await TestData.ProductStockAsync(context, productId));
        Assert.Equal(1, _factory.Provider.AuthCalls);
        Assert.Single(await Outbox(context), m => m.Type == OutboxType.OrderPlaced);
    }

    [Fact]
    public async Task Tutar_siparis_toplamiyla_uyusmazsa_reddedilir_ve_siparis_iptal_edilir()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());
        _factory.Provider.PaidPrice = payment.Amount - 1m;

        var response = await PostCallbackAsync(client, payment);

        Assert.Equal("/odeme", response.Headers.Location?.OriginalString);
        Assert.Equal(OrderStatus.IptalEdildi, Assert.Single(await new EfOrderDal(context).GetListAsync()).Status);
        Assert.Equal(PaymentStatus.Basarisiz, (await new EfPaymentDal(context).GetAsync(p => p.Id == payment.Id))!.Status);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
    }

    [Fact]
    public async Task Imzasi_tutmayan_donus_reddedilir_cekim_yapilmaz()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());
        _factory.Provider.SignatureValid = false;

        var response = await PostCallbackAsync(client, payment);

        Assert.Equal("/odeme", response.Headers.Location?.OriginalString);
        Assert.Equal(0, _factory.Provider.AuthCalls);
        Assert.Equal(OrderStatus.IptalEdildi, Assert.Single(await new EfOrderDal(context).GetListAsync()).Status);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
    }

    /// <summary>KAPANIŞ-2 F-CARD: ödemesi tamamlanmamış kartlı siparişte stok hiç düşmemiştir; iptal stoğu şişirmez ve
    /// sonradan gelen 3D dönüşü iptal edilmiş siparişten çekim yapmaz.</summary>
    [Fact]
    public async Task Odenmemis_kart_siparisi_iptal_edilince_stok_artmaz_ve_gec_donus_cekim_yapmaz()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());
        var admin = await _factory.CreateSignedInClientAsync();

        var cancel = await HtmlForm.PostAsync(admin, $"/admin/orders/detail/{payment.OrderId}", "/admin/orders/changestatus", new Dictionary<string, string>
        {
            ["id"] = payment.OrderId.ToString(),
            ["next"] = ((int)OrderStatus.IptalEdildi).ToString()
        });
        Assert.Equal(HttpStatusCode.Found, cancel.StatusCode);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));

        var late = await PostCallbackAsync(_factory.CreateNonRedirectingClient(), payment);

        Assert.Equal("/odeme", late.Headers.Location?.OriginalString);
        Assert.Equal(0, _factory.Provider.AuthCalls);
        Assert.Equal(PaymentStatus.Basarisiz, (await new EfPaymentDal(context).GetAsync(p => p.Id == payment.Id))!.Status);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
    }

    /// <summary>KAPANIŞ-2 F-CARD: parası alınmamış kartlı sipariş onaylanıp kargoya verilemez.</summary>
    [Fact]
    public async Task Odenmemis_kart_siparisi_onaylanamaz()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());
        var admin = await _factory.CreateSignedInClientAsync();

        var approve = await HtmlForm.PostAsync(admin, $"/admin/orders/detail/{payment.OrderId}", "/admin/orders/changestatus", new Dictionary<string, string>
        {
            ["id"] = payment.OrderId.ToString(),
            ["next"] = ((int)OrderStatus.Onaylandi).ToString()
        });

        Assert.Equal(HttpStatusCode.BadRequest, approve.StatusCode);
        Assert.Equal(OrderStatus.Beklemede, Assert.Single(await new EfOrderDal(context).GetListAsync()).Status);
    }

    [Fact]
    public async Task Bilinmeyen_conversation_id_404_alir()
    {
        var response = await PostCallbackAsync(_factory.CreateNonRedirectingClient(), new Payment { ConversationId = "yok-boyle-bir-kayit" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Donus_antiforgerysiz_calisir_otuz_birinci_istekte_429_alir()
    {
        var client = _factory.CreateNonRedirectingClient();
        var unknown = new Payment { ConversationId = "yok-boyle-bir-kayit" };

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await PostCallbackAsync(client, unknown)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostCallbackAsync(client, unknown)).StatusCode);
    }

    [Fact]
    public async Task Donus_adresine_get_405_alir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(Callback);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Saklanan_ham_yanitta_kart_numarasi_ve_cvc_yoktur()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);

        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);

        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());
        Assert.NotNull(payment.RawResponse);
        Assert.Contains("\"status\":\"success\"", payment.RawResponse);
        Assert.DoesNotContain(CardNumber, payment.RawResponse);
        Assert.DoesNotContain("\"cvc\":\"123\"", payment.RawResponse);
    }

    [Fact]
    public async Task Kart_bilgisi_eksikse_siparis_acilmaz()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);

        var response = await PostCheckoutAsync(client, PaymentMethod.KrediKarti, cardNumber: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await new EfOrderDal(context).GetListAsync());
        Assert.Empty(_factory.Provider.Inits);
    }

    [Fact]
    public async Task Odeme_baslatilamazsa_siparis_iptal_edilir_sepet_kalir()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);
        _factory.Provider.InitFails = true;

        var response = await PostCheckoutAsync(client, PaymentMethod.KrediKarti);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Contains("Kart limiti yetersiz.", await response.Content.ReadAsStringAsync());
        Assert.Equal(OrderStatus.IptalEdildi, Assert.Single(await new EfOrderDal(context).GetListAsync()).Status);
        Assert.Equal(PaymentStatus.Basarisiz, Assert.Single(await new EfPaymentDal(context).GetListAsync()).Status);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
        Assert.NotEmpty(await new EfCartItemDal(context).GetListAsync());
    }

    [Theory]
    [InlineData(PaymentMethod.KapidaOdeme)]
    [InlineData(PaymentMethod.HavaleEft)]
    public async Task Kapida_ve_havale_akisi_bozulmaz_stok_aninda_duser_odeme_kaydi_acilmaz(PaymentMethod method)
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        var productId = await FillCartAsync(context, client);

        var response = await PostCheckoutAsync(client, method);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(4, await TestData.ProductStockAsync(context, productId));
        Assert.Empty(await new EfPaymentDal(context).GetListAsync());
        Assert.Empty(_factory.Provider.Inits);
        Assert.Contains(await Outbox(context), m => m.Type == OutboxType.OrderPlaced && m.To == Email);
        Assert.Empty(await new EfCartItemDal(context).GetListAsync());
    }

    [Theory]
    [InlineData(450, 529.90)]
    [InlineData(2600, 2600)]
    public async Task Kargo_esigi_kart_tutarina_da_uygulanir(decimal price, decimal expected)
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client, price);

        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);

        Assert.Equal(expected, Assert.Single(await new EfPaymentDal(context).GetListAsync()).Amount);
        Assert.Equal(expected, Assert.Single(_factory.Provider.Inits).Order.Total);
    }

    [Fact]
    public async Task Tesekkur_sayfasi_kart_siparisinde_odeme_satirini_gosterir()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());
        var location = (await PostCallbackAsync(client, payment)).Headers.Location!.OriginalString;

        var html = await (await client.GetAsync(location)).Content.ReadAsStringAsync();

        Assert.Contains("Kredi kartı", html);
        Assert.Contains("Ödeme alındı", html);
    }

    [Fact]
    public async Task Admin_siparis_detayinda_odeme_durumu_saglayici_ve_payment_id_gorunur()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);
        await PostCheckoutAsync(client, PaymentMethod.KrediKarti);
        var payment = Assert.Single(await new EfPaymentDal(context).GetListAsync());
        await PostCallbackAsync(client, payment);
        var admin = await _factory.CreateSignedInClientAsync();

        var html = await (await admin.GetAsync($"/admin/orders/detail/{payment.OrderId}")).Content.ReadAsStringAsync();

        Assert.Contains("pay-1", html);
        Assert.Contains("iyzico", html);
        Assert.Contains("Ödeme alındı", html);
        Assert.DoesNotContain("İade et", html);
    }

    [Fact]
    public async Task Csp_iyzico_alani_yalniz_odeme_sayfasinda_yer_alir()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);

        var checkout = Csp(await client.GetAsync("/odeme"));
        Assert.Contains("form-action 'self' https://sandbox-api.iyzipay.com", checkout);
        Assert.Contains("frame-src https://sandbox-api.iyzipay.com", checkout);

        foreach (var url in new[] { "/", "/sepet", "/urun/celik-tencere" })
        {
            Assert.DoesNotContain("iyzipay", Csp(await client.GetAsync(url)));
        }
    }

    private static string Csp(HttpResponseMessage response) => string.Join(' ', response.Headers.GetValues("Content-Security-Policy"));

    private static Task<List<OutboxMessage>> Outbox(HerYerdeContext context) => new EfOutboxMessageDal(context).GetListAsync();

    private static async Task<int> FillCartAsync(HerYerdeContext context, HttpClient client, decimal price = 450m)
    {
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: price, stock: 5);
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });
        return productId;
    }

    private static Task<HttpResponseMessage> PostCheckoutAsync(HttpClient client, PaymentMethod method, string cardNumber = CardNumber)
        => HtmlForm.PostAsync(client, "/odeme", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Email"] = Email,
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)method).ToString(),
            ["CardHolderName"] = "AYSE YILMAZ",
            ["CardNumber"] = cardNumber,
            ["CardExpireMonth"] = "12",
            ["CardExpireYear"] = "2030",
            ["CardCvc"] = "123",
            ["LegalConsent"] = "true"
        });

    private static Task<HttpResponseMessage> PostCallbackAsync(HttpClient client, Payment payment, string status = "success", string mdStatus = "1")
        => client.PostAsync(Callback, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["status"] = status,
            ["paymentId"] = payment.PaymentId ?? "pay-0",
            ["conversationId"] = payment.ConversationId,
            ["conversationData"] = "3ds-veri",
            ["mdStatus"] = mdStatus,
            ["signature"] = "imza"
        }));
}

/// <summary>İyzico anahtarları dolu; sağlayıcı sahte.</summary>
public sealed class CardPaymentFactory : AdminWebFactory
{
    public FakePaymentProvider Provider { get; } = new();

    protected override void Configure(Dictionary<string, string?> settings)
    {
        settings["Iyzico:ApiKey"] = "sandbox-api-key";
        settings["Iyzico:SecretKey"] = "sandbox-secret-key";
        settings["Iyzico:CspSources:0"] = "https://sandbox-api.iyzipay.com";
    }

    protected override void ConfigureServices(IServiceCollection services)
        => services.AddSingleton<IPaymentProvider>(Provider);
}

/// <summary>Anahtar yok: kartla ödeme kapalı olmalı.</summary>
public sealed class NoCardKeyFactory : AdminWebFactory
{
    protected override void Configure(Dictionary<string, string?> settings)
    {
        settings["Iyzico:ApiKey"] = "";
        settings["Iyzico:SecretKey"] = "";
    }
}
