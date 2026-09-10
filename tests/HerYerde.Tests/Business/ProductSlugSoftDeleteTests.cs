using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

/// <summary>B04: silinen ürünün slug'ı sorgu süzgeci arkasında kalıp benzersizlik kontrolünü şaşırtmaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductSlugSoftDeleteTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Silinen_urunle_ayni_adli_urun_ikinci_slugu_alir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewProductManager(context);
        var categoryId = await TestData.AddRootCategoryAsync(context, "Ev", "ev");
        var (_, first) = await manager.AddAsync(TestData.NewProduct(categoryId, "Çelik Tencere", string.Empty));
        await manager.DeleteAsync(first.Data!.Id);

        var (status, second) = await manager.AddAsync(TestData.NewProduct(categoryId, "Çelik Tencere", string.Empty));

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("celik-tencere", first.Data.Slug);
        Assert.Equal("celik-tencere-2", second.Data!.Slug);
    }

    [Fact]
    public async Task Silinen_urunun_stok_kodu_yeni_varyantta_kullanilamaz()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewProductManager(context);
        var (productId, _) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar");
        var categoryId = (await new EfProductDal(context).GetAsync(p => p.Id == productId))!.CategoryId;
        await manager.DeleteAsync(productId);
        var replacement = await TestData.AddProductAsync(context, categoryId, "Şalvar İkinci", "salvar-ikinci");

        var (status, _) = await manager.AddVariantAsync(new ProductVariant
        {
            ProductId = replacement,
            Sku = "SALVAR-M",
            Stock = 1
        });

        Assert.Equal(HttpStatusCode.Conflict, status);
    }
}
