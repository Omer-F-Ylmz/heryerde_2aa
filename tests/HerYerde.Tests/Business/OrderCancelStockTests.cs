using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>B03: iptal edilen siparişin varyant stoğu geri döner, iki kez dönmez.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderCancelStockTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Iptal_edilince_varyant_stogu_eski_degerine_doner()
    {
        await using var context = TestDb.NewContext();
        var (orderId, variantId) = await PlaceClothingOrderAsync(context, stock: 3, quantity: 2);
        Assert.Equal(1, await StockAsync(context, variantId));

        var (status, _) = await TestData.NewOrderManager(context).ChangeStatusAsync(orderId, OrderStatus.IptalEdildi);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(3, await StockAsync(context, variantId));
    }

    [Fact]
    public async Task Ikinci_iptal_denemesi_stogu_bir_daha_arttirmaz()
    {
        await using var context = TestDb.NewContext();
        var (orderId, variantId) = await PlaceClothingOrderAsync(context, stock: 3, quantity: 2);
        var manager = TestData.NewOrderManager(context);
        await manager.ChangeStatusAsync(orderId, OrderStatus.IptalEdildi);

        var (status, _) = await manager.ChangeStatusAsync(orderId, OrderStatus.IptalEdildi);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(3, await StockAsync(context, variantId));
    }

    [Fact]
    public async Task Varyantsiz_urunun_iptali_stok_islemi_yapmaz()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cart.AddAsync(cartId, productId, variantId: null, quantity: 2);
        var orders = TestData.NewOrderManager(context);
        var (_, placed) = await orders.PlaceAsync(cartId, Draft());

        var (status, _) = await orders.ChangeStatusAsync(placed.Data!.Id, OrderStatus.IptalEdildi);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    private static async Task<(int OrderId, int VariantId)> PlaceClothingOrderAsync(
        HerYerdeContext context,
        int stock,
        int quantity)
    {
        var (productId, variantId) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: stock);
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cart.AddAsync(cartId, productId, variantId, quantity);

        var (_, placed) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft());
        return (placed.Data!.Id, variantId);
    }

    private static async Task<int> StockAsync(HerYerdeContext context, int variantId)
        => (await new EfProductVariantDal(context).GetAsync(v => v.Id == variantId))!.Stock;

    private static OrderDraft Draft() => new(
        "Ayşe Yılmaz",
        "0542 497 09 82",
        null,
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        null,
        PaymentMethod.KapidaOdeme);
}
