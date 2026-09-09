using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.DataAccess;

[Collection(DatabaseCollection.Name)]
public sealed class ProductSoftDeleteTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Soft_delete_edilen_urun_sorgulardan_dusulur_ama_satir_durur()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context);
        await TestData.AddProductAsync(context, categoryId, "Şile Bezi Şalvar", "sile-bezi-salvar");
        var dal = new EfProductDal(context);
        var unitOfWork = new EfUnitOfWork(context);

        var product = await dal.GetTrackedAsync(p => p.Slug == "sile-bezi-salvar");
        Assert.NotNull(product);
        product.DeletedAt = DateTime.UtcNow;
        dal.Update(product);
        await unitOfWork.SaveChangesAsync();

        var visible = await dal.GetListAsync();
        var all = await context.Products.IgnoreQueryFilters().CountAsync();

        Assert.Empty(visible);
        Assert.Equal(1, all);
    }
}
