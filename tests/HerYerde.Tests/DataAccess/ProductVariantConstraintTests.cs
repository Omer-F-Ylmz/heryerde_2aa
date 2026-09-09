using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.DataAccess;

[Collection(DatabaseCollection.Name)]
public sealed class ProductVariantConstraintTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ProductVariant NewVariant(int productId, string sku, int stock) => new()
    {
        ProductId = productId,
        Size = "M",
        Color = "Vizon",
        Sku = sku,
        Stock = stock
    };

    [Fact]
    public async Task Ayni_sku_ile_ikinci_varyant_unique_index_tarafindan_reddedilir()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context);
        var productId = await TestData.AddProductAsync(context, categoryId, "Şile Bezi Şalvar", "sile-bezi-salvar");
        var dal = new EfProductVariantDal(context);
        var unitOfWork = new EfUnitOfWork(context);

        await dal.AddAsync(NewVariant(productId, "HY-SLV-M-VZN", 5));
        await unitOfWork.SaveChangesAsync();

        await dal.AddAsync(NewVariant(productId, "HY-SLV-M-VZN", 3));

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task Negatif_stok_check_constraint_tarafindan_reddedilir()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context);
        var productId = await TestData.AddProductAsync(context, categoryId, "Şile Bezi Şalvar", "sile-bezi-salvar");
        var dal = new EfProductVariantDal(context);
        var unitOfWork = new EfUnitOfWork(context);

        await dal.AddAsync(NewVariant(productId, "HY-SLV-S-VZN", -1));

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task Ev_urunu_beden_ve_renk_bos_birakilarak_eklenebilir()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context, "Ev", "ev");
        var productId = await TestData.AddProductAsync(context, categoryId, "Bambu Saklama Kabı", "bambu-saklama-kabi");
        var dal = new EfProductVariantDal(context);
        var unitOfWork = new EfUnitOfWork(context);

        await dal.AddAsync(new ProductVariant { ProductId = productId, Sku = "HY-EV-SAK-01", Stock = 12 });
        await unitOfWork.SaveChangesAsync();

        var saved = await dal.GetAsync(v => v.Sku == "HY-EV-SAK-01");

        Assert.NotNull(saved);
        Assert.Null(saved.Size);
        Assert.Null(saved.Color);
    }
}
