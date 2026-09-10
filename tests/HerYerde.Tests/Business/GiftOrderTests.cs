using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>G09: "1 alana 1 hediye" siparişe 0 ₺ satır olarak girer, hediye stoğu da düşer.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class GiftOrderTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ayni_urun_hediyesi_siparise_sifir_liralik_satir_ekler()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 10);
        await TestData.SetGiftAsync(context, productId, GiftMode.AyniUrun);

        var orderId = await PlaceAsync(context, productId, quantity: 1);
        var gift = Assert.Single((await ItemsAsync(context, orderId)), i => i.IsGift);

        Assert.Equal(0m, gift.UnitPrice);
        Assert.Equal(1, gift.Quantity);
        Assert.Equal("Çelik Tencere", gift.ProductName);
    }

    [Fact]
    public async Task Baska_urun_hediyesi_o_urunun_adiyla_eklenir()
    {
        await using var context = TestDb.NewContext();
        var giftId = await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", stock: 4);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 10);
        await TestData.SetGiftAsync(context, productId, GiftMode.BaskaUrun, giftId);

        var orderId = await PlaceAsync(context, productId, quantity: 1);
        var gift = Assert.Single((await ItemsAsync(context, orderId)), i => i.IsGift);

        Assert.Equal("Hasır Sepet", gift.ProductName);
        Assert.Equal(0m, gift.UnitPrice);
    }

    [Fact]
    public async Task Hediye_stogu_da_duser()
    {
        await using var context = TestDb.NewContext();
        var giftId = await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", stock: 4);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 10);
        await TestData.SetGiftAsync(context, productId, GiftMode.BaskaUrun, giftId, giftQty: 2);

        await PlaceAsync(context, productId, quantity: 1);

        Assert.Equal(2, await TestData.ProductStockAsync(context, giftId));
    }

    [Fact]
    public async Task Kampanya_bittiyse_hediye_satiri_acilmaz()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 10);
        await TestData.SetGiftAsync(context, productId, GiftMode.AyniUrun, endsAt: TestClock.Now.AddSeconds(-1));

        var orderId = await PlaceAsync(context, productId, quantity: 1);

        Assert.DoesNotContain(await ItemsAsync(context, orderId), i => i.IsGift);
    }

    [Fact]
    public async Task Hediye_stogu_yoksa_satir_acilmaz_ve_mesajda_not_dusulur()
    {
        await using var context = TestDb.NewContext();
        var giftId = await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", stock: 0);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 10);
        await TestData.SetGiftAsync(context, productId, GiftMode.BaskaUrun, giftId);

        var (orderId, message) = await PlaceWithMessageAsync(context, productId, quantity: 1);

        Assert.DoesNotContain(await ItemsAsync(context, orderId), i => i.IsGift);
        Assert.Contains("Hasır Sepet", message);
        Assert.Contains("Hediye stoğu", message);
    }

    [Fact]
    public async Task Hediye_satiri_iptalde_stoga_geri_doner()
    {
        await using var context = TestDb.NewContext();
        var giftId = await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", stock: 4);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 10);
        await TestData.SetGiftAsync(context, productId, GiftMode.BaskaUrun, giftId);
        var orderId = await PlaceAsync(context, productId, quantity: 1);

        await TestData.NewOrderManager(context).ChangeStatusAsync(orderId, OrderStatus.IptalEdildi);

        Assert.Equal(4, await TestData.ProductStockAsync(context, giftId));
    }

    private static async Task<List<OrderItem>> ItemsAsync(HerYerdeContext context, int orderId)
        => await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == orderId);

    private static async Task<int> PlaceAsync(HerYerdeContext context, int productId, int quantity)
        => (await PlaceWithMessageAsync(context, productId, quantity)).OrderId;

    private static async Task<(int OrderId, string Message)> PlaceWithMessageAsync(
        HerYerdeContext context,
        int productId,
        int quantity)
    {
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        var (added, _) = await cart.AddAsync(cartId, productId, variantId: null, quantity: quantity);
        Assert.Equal(HttpStatusCode.OK, added);

        var (status, placed) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft());
        Assert.Equal(HttpStatusCode.Created, status);
        return (placed.Data!.Id, placed.Message);
    }

    private static OrderDraft Draft() => new(
        "Ayşe Yılmaz",
        "05001234567",
        null,
        "Örnek mahallesi 1. sokak no 2",
        "İstanbul",
        "Kadıköy",
        null,
        PaymentMethod.KapidaOdeme);
}
