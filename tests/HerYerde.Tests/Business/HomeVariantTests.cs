using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D10 A1: Ev ürününde varyant isteğe bağlı; varyant varsa stok varyantta (ürün stoğu boş), yoksa ürün stoğu.
/// Giyim kuralı (varyantsız yayına alınamaz) değişmez.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class HomeVariantTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ev_urunune_boy_ve_renk_eksenli_varyant_eklenir_urun_yayina_alinir()
    {
        await using var context = TestDb.NewContext();
        var products = TestData.NewProductManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Keten Masa Örtüsü", "keten-masa-ortusu");

        var (added, _) = await products.AddVariantAsync(new ProductVariant { ProductId = productId, Size = "160x220", Color = "Bej", Sku = "KMO-160-BEJ", Stock = 4 });
        var stored = (await products.GetByIdAsync(productId)).Item2.Data!;
        stored.VariantAxis1Label = "Boy";
        stored.VariantAxis2Label = "Renk";
        var (updated, _) = await products.UpdateAsync(stored);

        Assert.Equal(HttpStatusCode.Created, added);
        Assert.Equal(HttpStatusCode.OK, updated);
        var variant = Assert.Single(await new EfProductVariantDal(context).GetListAsync(v => v.ProductId == productId));
        Assert.Equal("160x220", variant.Size);
        Assert.Equal("Boy", (await products.GetByIdAsync(productId)).Item2.Data!.VariantAxis1Label);
    }

    [Fact]
    public async Task Varyantli_Ev_urununde_urun_stogu_bos_kalmak_zorunda()
    {
        await using var context = TestDb.NewContext();
        var products = TestData.NewProductManager(context);
        var stocked = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);

        var (addedOnStocked, addResult) = await products.AddVariantAsync(new ProductVariant { ProductId = stocked, Size = "24 cm", Sku = "TNC-24", Stock = 3 });

        var withVariant = await TestData.AddHomeProductAsync(context, "Keten Örtü", "keten-ortu");
        await products.AddVariantAsync(new ProductVariant { ProductId = withVariant, Color = "Bej", Sku = "KO-BEJ", Stock = 3 });
        var stored = (await products.GetByIdAsync(withVariant)).Item2.Data!;
        stored.Stock = 7;
        var (updated, updateResult) = await products.UpdateAsync(stored);

        Assert.Equal(HttpStatusCode.BadRequest, addedOnStocked);
        Assert.Contains("varyant", addResult.Message);
        Assert.Equal(HttpStatusCode.BadRequest, updated);
        Assert.Contains("varyant", updateResult.Message);
        Assert.Null(await TestData.ProductStockAsync(context, withVariant));
    }

    [Fact]
    public async Task Varyantsiz_Ev_urunu_eskisi_gibi_urun_stogunu_kullanir()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var products = TestData.NewProductManager(context);
        var stored = (await products.GetByIdAsync(productId)).Item2.Data!;
        stored.Stock = 4;

        var (updated, _) = await products.UpdateAsync(stored);
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        var (added, _) = await cart.AddAsync(cartId, productId, variantId: null, quantity: 2);

        Assert.Equal(HttpStatusCode.OK, updated);
        Assert.Equal(HttpStatusCode.OK, added);
        Assert.Equal(4, await TestData.ProductStockAsync(context, productId));
    }

    [Fact]
    public async Task Varyantli_Ev_urununde_siparis_varyant_stogunu_duser_iptal_geri_yukler()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Keten Örtü", "keten-ortu");
        await TestData.NewProductManager(context).AddVariantAsync(new ProductVariant { ProductId = productId, Color = "Bej", Sku = "KO-BEJ", Stock = 5 });
        var variant = Assert.Single(await new EfProductVariantDal(context).GetListAsync(v => v.ProductId == productId));

        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        var (withoutChoice, _) = await cart.AddAsync(cartId, productId, variantId: null, quantity: 1);
        var (added, _) = await cart.AddAsync(cartId, productId, variant.Id, quantity: 2);
        var orders = TestData.NewOrderManager(context);
        var (placed, order) = await orders.PlaceAsync(cartId, Draft());
        var afterPlace = await VariantStockAsync(context, variant.Id);

        var (cancelled, _) = await orders.ChangeStatusAsync(order.Data!.Id, OrderStatus.IptalEdildi);

        Assert.Equal(HttpStatusCode.BadRequest, withoutChoice);
        Assert.Equal(HttpStatusCode.OK, added);
        Assert.Equal(HttpStatusCode.Created, placed);
        Assert.Equal(3, afterPlace);
        Assert.Equal(HttpStatusCode.OK, cancelled);
        Assert.Equal(5, await VariantStockAsync(context, variant.Id));
        Assert.Null(await TestData.ProductStockAsync(context, productId));
    }

    [Fact]
    public async Task Giyim_urunu_Ev_kurali_degisse_de_varyantsiz_yayina_alinamaz()
    {
        await using var context = TestDb.NewContext();
        var products = TestData.NewProductManager(context);
        var giyim = await TestData.AddRootCategoryAsync(context, "Giyim", "giyim");

        var (status, result) = await products.AddAsync(new Product
        {
            Name = "Şile Bezi Şalvar",
            Description = "Pamuklu.",
            CategoryId = giyim,
            Price = 450m,
            IsActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("varyant", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> VariantStockAsync(HerYerdeContext context, int variantId)
        => (await new EfProductVariantDal(context).GetAsync(v => v.Id == variantId))!.Stock;

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
