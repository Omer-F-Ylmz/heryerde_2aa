using System.Net;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class CategoryManagerTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static CategoryManager NewManager(HerYerdeContext context)
        => new(new EfCategoryDal(context), new EfProductDal(context), new EfUnitOfWork(context));

    [Fact]
    public async Task Slug_addan_uretilir_ve_cakisirsa_2_ekiyle_ayrisir()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);

        await manager.AddAsync(new Category { Name = "Mutfak & Sofra", IsActive = true });
        await manager.AddAsync(new Category { Name = "Mutfak Sofra", IsActive = true });

        var (_, all) = await manager.GetAllAsync();
        Assert.Equal(
            ["mutfak-sofra", "mutfak-sofra-2"],
            all.Data!.OrderBy(c => c.Id).Select(c => c.Slug));
    }

    [Fact]
    public async Task Urunu_olan_kategori_silinmez_409_doner()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context);
        var childId = await AddChildAsync(context, categoryId, "Şalvar");
        await TestData.AddProductAsync(context, childId, "Şile Bezi Şalvar", "sile-bezi-salvar");

        var (status, result) = await NewManager(context).DeleteAsync(childId);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.False(result.Success);
        Assert.NotNull(await new EfCategoryDal(context).GetAsync(c => c.Id == childId));
    }

    [Fact]
    public async Task Alt_kategorisi_olan_kategori_silinmez_409_doner()
    {
        await using var context = TestDb.NewContext();
        var rootId = await TestData.AddRootCategoryAsync(context);
        await AddChildAsync(context, rootId, "Şalvar");

        var (status, _) = await NewManager(context).DeleteAsync(rootId);

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task Kok_kategori_bos_olsa_da_silinmez()
    {
        await using var context = TestDb.NewContext();
        var rootId = await TestData.AddRootCategoryAsync(context);

        var (status, result) = await NewManager(context).DeleteAsync(rootId);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("kök", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await new EfCategoryDal(context).GetAsync(c => c.Id == rootId));
    }

    [Fact]
    public async Task Kok_kategorinin_adi_degisse_de_slugu_sabit_kalir()
    {
        await using var context = TestDb.NewContext();
        var rootId = await TestData.AddRootCategoryAsync(context);

        var (status, _) = await NewManager(context).UpdateAsync(new Category
        {
            Id = rootId,
            Name = "Giyim & Aksesuar",
            SortOrder = 3,
            IsActive = true
        });

        var stored = await new EfCategoryDal(context).GetAsync(c => c.Id == rootId);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Giyim & Aksesuar", stored!.Name);
        Assert.Equal("giyim", stored.Slug);
    }

    [Fact]
    public async Task Alt_kategorinin_slugu_ad_degisince_yenilenir()
    {
        await using var context = TestDb.NewContext();
        var rootId = await TestData.AddRootCategoryAsync(context);
        var childId = await AddChildAsync(context, rootId, "Şalvar");

        await NewManager(context).UpdateAsync(new Category
        {
            Id = childId,
            Name = "Şalvar & Pantolon",
            ParentId = rootId,
            IsActive = true
        });

        var stored = await new EfCategoryDal(context).GetAsync(c => c.Id == childId);
        Assert.Equal("salvar-pantolon", stored!.Slug);
    }

    [Fact]
    public async Task Alt_kategorinin_altina_ucuncu_seviye_acilmaz()
    {
        await using var context = TestDb.NewContext();
        var rootId = await TestData.AddRootCategoryAsync(context);
        var childId = await AddChildAsync(context, rootId, "Şalvar");

        var (status, _) = await NewManager(context).AddAsync(new Category
        {
            Name = "Şile Şalvar",
            ParentId = childId,
            IsActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Null(await new EfCategoryDal(context).GetAsync(c => c.Name == "Şile Şalvar"));
    }

    private static async Task<int> AddChildAsync(HerYerdeContext context, int parentId, string name)
    {
        var (_, _) = await NewManager(context).AddAsync(new Category { Name = name, ParentId = parentId, IsActive = true });
        var child = await new EfCategoryDal(context).GetAsync(c => c.Name == name);
        return child!.Id;
    }
}
