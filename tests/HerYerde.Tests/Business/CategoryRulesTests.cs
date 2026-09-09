using HerYerde.Business.Rules;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class CategoryRulesTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Kok_kategori_ust_olabilir_alt_kategori_olamaz()
    {
        await using var context = TestDb.NewContext();
        var rootId = await TestData.AddRootCategoryAsync(context, "Ev", "ev");
        var dal = new EfCategoryDal(context);
        var unitOfWork = new EfUnitOfWork(context);

        await dal.AddAsync(new Category { Name = "Mutfak & Sofra", Slug = "mutfak-sofra", ParentId = rootId, SortOrder = 1, IsActive = true });
        await unitOfWork.SaveChangesAsync();

        var root = await dal.GetAsync(c => c.Slug == "ev");
        var child = await dal.GetAsync(c => c.Slug == "mutfak-sofra");

        Assert.NotNull(root);
        Assert.NotNull(child);
        Assert.True(CategoryRules.CanBeParent(root));
        Assert.False(CategoryRules.CanBeParent(child));
    }
}
