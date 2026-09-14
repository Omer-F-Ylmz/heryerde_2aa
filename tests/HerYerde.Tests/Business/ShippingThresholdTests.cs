using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class ShippingThresholdTests : IAsyncLifetime
{
    private static readonly OrderDraft Draft = new(
        "Ayşe Yılmaz",
        "0542 497 09 82",
        null,
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        null,
        PaymentMethod.KapidaOdeme);

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Esik_altinda_sepette_kargo_ucreti_alinir()
    {
        await using var context = TestDb.NewContext();

        var cart = await CartAsync(context, price: 450m, quantity: 2);

        Assert.Equal(900m, cart.Subtotal);
        Assert.Equal(TestData.ShippingFee, cart.ShippingFee);
        Assert.False(cart.FreeShipping);
        Assert.Equal(TestData.FreeShippingOver - 900m, cart.ToFreeShipping);
    }

    [Fact]
    public async Task Esik_ustunde_sepette_kargo_bedava()
    {
        await using var context = TestDb.NewContext();

        var cart = await CartAsync(context, price: 450m, quantity: 6);

        Assert.Equal(2700m, cart.Subtotal);
        Assert.Equal(0m, cart.ShippingFee);
        Assert.Equal(2700m, cart.Total);
        Assert.True(cart.FreeShipping);
        Assert.Equal(0m, cart.ToFreeShipping);
    }

    [Fact]
    public async Task Esik_tam_sinirinda_kargo_bedava()
    {
        await using var context = TestDb.NewContext();

        var cart = await CartAsync(context, price: 500m, quantity: 5);

        Assert.Equal(TestData.FreeShippingOver, cart.Subtotal);
        Assert.Equal(0m, cart.ShippingFee);
        Assert.True(cart.FreeShipping);
    }

    [Fact]
    public async Task Siparis_toplami_esikle_uyumludur()
    {
        await using var context = TestDb.NewContext();
        var below = await PlaceAsync(context, price: 450m, quantity: 2, slug: "celik-tencere");
        var above = await PlaceAsync(context, price: 450m, quantity: 6, slug: "cam-surahi");

        Assert.Equal(TestData.ShippingFee, below.ShippingFee);
        Assert.Equal(900m + TestData.ShippingFee, below.Total);
        Assert.Equal(0m, above.ShippingFee);
        Assert.Equal(2700m, above.Total);
    }

    [Fact]
    public async Task Esik_kapaliyken_kargo_her_zaman_ucretlidir()
    {
        await using var context = TestDb.NewContext();

        var cart = await CartAsync(context, price: 450m, quantity: 6, freeShippingOver: 0m);

        Assert.Equal(TestData.ShippingFee, cart.ShippingFee);
        Assert.False(cart.FreeShipping);
        Assert.Equal(0m, cart.ToFreeShipping);
    }

    private static async Task<CartView> CartAsync(
        HerYerdeContext context,
        decimal price,
        int quantity,
        decimal? freeShippingOver = null,
        string slug = "celik-tencere")
    {
        var cartManager = TestData.NewCartManager(context, freeShippingOver: freeShippingOver);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", slug, price);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity);
        return (await cartManager.GetAsync(cartId)).Item2.Data!;
    }

    private static async Task<Entities.Concrete.Order> PlaceAsync(
        HerYerdeContext context,
        decimal price,
        int quantity,
        string slug)
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Ürün " + slug, slug, price);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity);

        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft);
        Assert.Equal(HttpStatusCode.Created, status);
        return result.Data!;
    }
}
