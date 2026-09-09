using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.DataAccess;

[Collection(DatabaseCollection.Name)]
public sealed class ProductSlugUniqueTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ayni_slug_ile_ikinci_urun_unique_index_tarafindan_reddedilir()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context);
        var dal = new EfProductDal(context);
        var unitOfWork = new EfUnitOfWork(context);

        await dal.AddAsync(TestData.NewProduct(categoryId, "Şile Bezi Şalvar", "sile-bezi-salvar"));
        await unitOfWork.SaveChangesAsync();

        await dal.AddAsync(TestData.NewProduct(categoryId, "Şile Bezi Şalvar (kopya)", "sile-bezi-salvar"));

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
    }
}
