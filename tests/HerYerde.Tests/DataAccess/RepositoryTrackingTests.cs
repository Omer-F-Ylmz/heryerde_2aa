using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.DataAccess;

[Collection(DatabaseCollection.Name)]
public sealed class RepositoryTrackingTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAsync_kaydi_izlemez_GetTrackedAsync_izler()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context);
        await TestData.AddProductAsync(context, categoryId, "Şile Bezi Şalvar", "sile-bezi-salvar");
        context.ChangeTracker.Clear();
        var dal = new EfProductDal(context);

        var readOnly = await dal.GetAsync(p => p.Slug == "sile-bezi-salvar");
        Assert.NotNull(readOnly);
        Assert.Equal(EntityState.Detached, context.Entry(readOnly).State);

        context.ChangeTracker.Clear();

        var tracked = await dal.GetTrackedAsync(p => p.Slug == "sile-bezi-salvar");
        Assert.NotNull(tracked);
        Assert.Equal(EntityState.Unchanged, context.Entry(tracked).State);
    }
}
