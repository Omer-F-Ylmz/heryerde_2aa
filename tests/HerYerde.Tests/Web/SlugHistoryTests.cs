using System.Net;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D10 A5: ürün/kategori slug'ı değişince eski adres 301 ile yenisine gider; aynı eski slug tek satırdır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SlugHistoryTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Urun_adi_degisince_eski_adres_301_ile_yeni_adrese_gider()
    {
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            await RenameProductAsync(context, productId, "Granit Tencere");
        }

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/urun/celik-tencere");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/urun/granit-tencere", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateNonRedirectingClient().GetAsync("/urun/hic-olmadi")).StatusCode);
    }

    [Fact]
    public async Task Alt_kategori_adi_degisince_eski_adres_301()
    {
        await using (var context = TestDb.NewContext())
        {
            var childId = await TestData.AddChildCategoryAsync(context, "Mutfak", "mutfak");
            var categories = new HerYerde.Business.Concrete.CategoryManager(
                new HerYerde.DataAccess.Concrete.EntityFramework.EfCategoryDal(context),
                new HerYerde.DataAccess.Concrete.EntityFramework.EfProductDal(context),
                new HerYerde.DataAccess.Concrete.EntityFramework.EfSlugHistoryDal(context),
                new HerYerde.DataAccess.Concrete.EntityFramework.EfUnitOfWork(context));
            var stored = (await categories.GetByIdAsync(childId)).Item2.Data!;
            stored.Name = "Mutfak Sofra";
            Assert.Equal(HttpStatusCode.OK, (await categories.UpdateAsync(stored)).Item1);
        }

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/ev/mutfak?sirala=fiyat");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/ev/mutfak-sofra?sirala=fiyat", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Ayni_eski_slug_ikinci_kez_kaydedilince_tek_satir_kalir_ve_son_sahibini_gosterir()
    {
        await using var context = TestDb.NewContext();
        var first = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        await RenameProductAsync(context, first, "Granit Tencere");
        var second = await TestData.AddHomeProductAsync(context, "Döküm Tava", "dokum-tava");
        await RenameProductAsync(context, second, "Çelik Tencere");
        await RenameProductAsync(context, second, "Bakır Tencere");

        var rows = await context.Set<SlugHistory>().AsNoTracking().Where(h => h.OldSlug == "celik-tencere").ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(second, row.EntityId);
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/urun/celik-tencere");
        Assert.Equal("/urun/bakir-tencere", response.Headers.Location!.OriginalString);
    }

    private static async Task RenameProductAsync(HerYerde.DataAccess.Concrete.EntityFramework.Contexts.HerYerdeContext context, int productId, string name)
    {
        var products = TestData.NewProductManager(context);
        var stored = (await products.GetByIdAsync(productId)).Item2.Data!;
        stored.Name = name;
        Assert.Equal(HttpStatusCode.OK, (await products.UpdateAsync(stored)).Item1);
    }
}
