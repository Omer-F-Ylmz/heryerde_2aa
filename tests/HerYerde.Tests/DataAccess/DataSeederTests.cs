using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.DataAccess;

[Collection(DatabaseCollection.Name)]
public sealed class DataSeederTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Bos_veritabanina_6_ev_5_ortu_alt_kategorisi_18_urun_yazar_ikinci_kosuda_tekrar_yazmaz()
    {
        await using var context = TestDb.NewContext();
        await DataSeeder.SeedCatalogAsync(context);

        var ev = await context.Categories.SingleAsync(c => c.Slug == "ev" && c.ParentId == null);
        var giyim = await context.Categories.SingleAsync(c => c.Slug == "giyim" && c.ParentId == null);
        Assert.Equal(6, await context.Categories.CountAsync(c => c.ParentId == ev.Id));
        Assert.Equal(5, await context.Categories.CountAsync(c => c.ParentId == giyim.Id));
        Assert.Equal(18, await context.Products.CountAsync());
        Assert.Equal(3, await context.Products.CountAsync(p => p.CampaignPrice != null));

        await using var second = TestDb.NewContext();
        await DataSeeder.SeedCatalogAsync(second);

        Assert.Equal(13, await second.Categories.CountAsync());
        Assert.Equal(18, await second.Products.CountAsync());
    }

    /// <summary>GÖZ-FIX-2: açılış kataloğu yalnız Development'ta tohumlanır; Production'da veri elle girilir.</summary>
    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, false)]
    [InlineData("Staging", true, false)]
    public void Katalog_yalniz_development_ortaminda_tohumlanir(string environment, bool enabled, bool expected)
        => Assert.Equal(expected, DataSeeder.ShouldSeed(environment, enabled));
}
