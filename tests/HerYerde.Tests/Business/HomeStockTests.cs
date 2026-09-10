using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>G08: varyantsız (Ev) ürün de stok tutar; sipariş düşer, iptal iade eder, sepet aşamaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class HomeStockTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ev_urununde_siparis_stogu_duser()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);

        await PlaceAsync(context, productId, quantity: 2);

        Assert.Equal(3, await TestData.ProductStockAsync(context, productId));
    }

    [Fact]
    public async Task Ev_urununde_iptal_stogu_geri_yukler()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var orderId = await PlaceAsync(context, productId, quantity: 2);

        var (status, _) = await TestData.NewOrderManager(context).ChangeStatusAsync(orderId, OrderStatus.IptalEdildi);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
    }

    [Fact]
    public async Task Stok_tutmayan_ev_urunu_siparisten_sonra_da_null_kalir()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet");

        await PlaceAsync(context, productId, quantity: 3);

        Assert.Null(await TestData.ProductStockAsync(context, productId));
    }

    [Fact]
    public async Task Stogu_sifir_olan_ev_urunu_sepete_eklenemez()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 0);
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;

        var (status, result) = await cart.AddAsync(cartId, productId, variantId: null, quantity: 1);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("Bu ürün tükendi.", result.Message);
    }

    [Fact]
    public async Task Giyim_urununde_urun_stogu_bos_kalmak_zorunda()
    {
        await using var context = TestDb.NewContext();
        var (productId, _) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar");
        var products = TestData.NewProductManager(context);
        var stored = (await products.GetByIdAsync(productId)).Item2.Data!;
        stored.Stock = 7;

        var (status, result) = await products.UpdateAsync(stored);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("varyantta", result.Message);
        Assert.Null(await TestData.ProductStockAsync(context, productId));
    }

    private static async Task<int> PlaceAsync(HerYerdeContext context, int productId, int quantity)
    {
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        var (added, _) = await cart.AddAsync(cartId, productId, variantId: null, quantity: quantity);
        Assert.Equal(HttpStatusCode.OK, added);

        var (status, placed) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft());
        Assert.Equal(HttpStatusCode.Created, status);
        return placed.Data!.Id;
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
