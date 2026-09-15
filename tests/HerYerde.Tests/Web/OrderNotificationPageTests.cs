using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D6: yeni sipariş rozeti, WhatsApp kısayolu, kargo takibi ve kargo eşiği sayfada.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderNotificationPageTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Okunmamis_siparis_sayaci_baslikta_gorunur_detay_acilinca_duser()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context);
        var client = await _factory.CreateSignedInClientAsync();

        var before = await (await client.GetAsync("/admin/orders")).Content.ReadAsStringAsync();
        Assert.Contains("data-unseen-count=\"1\"", before);
        Assert.Contains("order-row__new", before);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/admin/orders/detail/{order.Id}")).StatusCode);

        var after = await (await client.GetAsync("/admin/orders")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-unseen-count", after);
        Assert.DoesNotContain("order-row__new", after);
        Assert.Equal(TestClock.Now, (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.SeenAt);
    }

    [Fact]
    public async Task Siparis_detayinda_whatsapp_baglantisi_uluslararasi_telefonla_kurulur()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context);
        var client = await _factory.CreateSignedInClientAsync();

        var html = await (await client.GetAsync($"/admin/orders/detail/{order.Id}")).Content.ReadAsStringAsync();

        Assert.Contains("https://wa.me/905424970982?text=", html);
        Assert.Contains(Uri.EscapeDataString(order.OrderNo), html);
        // Ham 0'lı numara bağlantıya girmez.
        Assert.DoesNotContain("wa.me/05424970982", html);
    }

    [Fact]
    public async Task Kargoya_verilen_siparisin_takip_baglantisi_tesekkur_sayfasinda_cikar()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context);
        var admin = await _factory.CreateSignedInClientAsync();

        await Advance(admin, order.Id, OrderStatus.Onaylandi);
        await Advance(admin, order.Id, OrderStatus.Hazirlaniyor);
        var shipped = await Advance(admin, order.Id, OrderStatus.Kargoda, TestData.Carrier, "1234567890");
        Assert.Equal(HttpStatusCode.Found, shipped.StatusCode);

        var client = _factory.CreateNonRedirectingClient();
        var html = await (await client.GetAsync(
            $"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync();

        Assert.Contains(TestData.Carrier, html);
        Assert.Contains("1234567890", html);
        Assert.Contains("https://kargo.test/takip/1234567890", html);
    }

    [Fact]
    public async Task Kargoda_gecisi_takip_nosuz_400_doner()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context);
        var admin = await _factory.CreateSignedInClientAsync();
        await Advance(admin, order.Id, OrderStatus.Onaylandi);
        await Advance(admin, order.Id, OrderStatus.Hazirlaniyor);

        var response = await Advance(admin, order.Id, OrderStatus.Kargoda);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OrderStatus.Hazirlaniyor, (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.Status);
    }

    [Fact]
    public async Task Sepette_esige_kalan_tutar_ilerleme_cubugunda_yazar()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m);
        var client = _factory.CreateNonRedirectingClient();
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "2"
        });

        var html = await (await client.GetAsync("/sepet")).Content.ReadAsStringAsync();

        // Lira işareti HtmlEncoder'ın aralığı dışında kaldığı için sayı ve cümle ayrı doğrulanır.
        Assert.Contains("1.600,00", html);
        Assert.Contains("daha ekleyin, kargo bizden", html);
        Assert.Contains("data-free-shipping-progress=\"36\"", html);
    }

    [Fact]
    public async Task Esik_asilinca_sepette_kargo_bedava_yazar()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m);
        var client = _factory.CreateNonRedirectingClient();
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "6"
        });

        var html = await (await client.GetAsync("/sepet")).Content.ReadAsStringAsync();

        Assert.Contains("Kargo bedava", html);
        Assert.DoesNotContain("daha ekleyin, kargo bizden", html);
    }

    private static Task<HttpResponseMessage> Advance(
        HttpClient client,
        int orderId,
        OrderStatus next,
        string? carrier = null,
        string? trackingNo = null)
    {
        var fields = new Dictionary<string, string> { ["next"] = ((int)next).ToString() };
        if (carrier is not null)
        {
            fields["carrier"] = carrier;
        }

        if (trackingNo is not null)
        {
            fields["trackingNo"] = trackingNo;
        }

        return HtmlForm.PostAsync(
            client,
            $"/admin/orders/detail/{orderId}",
            $"/admin/orders/changestatus/{orderId}",
            fields);
    }

    private static async Task<Order> PlaceOrderAsync(HerYerdeContext context)
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);

        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz",
            "0542 497 09 82",
            "ayse@example.com",
            "Cumhuriyet Mah. 12/3",
            "İstanbul",
            "Kadıköy",
            null,
            PaymentMethod.KapidaOdeme));

        Assert.Equal(HttpStatusCode.Created, status);
        return result.Data!;
    }
}
