using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.DataAccess;

[Collection(DatabaseCollection.Name)]
public sealed class DataSeederTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Bos_veritabanina_6_alt_kategori_18_urun_yazar_ikinci_kosuda_tekrar_yazmaz()
    {
        await using var context = TestDb.NewContext();
        await DataSeeder.SeedCatalogAsync(context);

        var ev = await context.Categories.SingleAsync(c => c.Slug == "ev" && c.ParentId == null);
        Assert.NotNull(await context.Categories.SingleOrDefaultAsync(c => c.Slug == "giyim" && c.ParentId == null));
        Assert.Equal(6, await context.Categories.CountAsync(c => c.ParentId == ev.Id));
        Assert.Equal(18, await context.Products.CountAsync());
        Assert.Equal(3, await context.Products.CountAsync(p => p.CampaignPrice != null));

        await using var second = TestDb.NewContext();
        await DataSeeder.SeedCatalogAsync(second);

        Assert.Equal(8, await second.Categories.CountAsync());
        Assert.Equal(18, await second.Products.CountAsync());
    }
}
