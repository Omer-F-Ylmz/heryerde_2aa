using System.Net;
using System.Text.RegularExpressions;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>Vitrin seçim kuralları SQL'e taşındıktan sonra: sıralama, hero ve benzer ürünler.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class StoreCatalogSqlTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync() => await TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<int> AddAsync(HerYerdeContext context, int categoryId, string slug, decimal price, decimal? campaign = null, DateTime? endsAt = null, bool active = true, int ageDays = 0)
    {
        var product = TestData.NewProduct(categoryId, "Ürün " + slug, slug);
        product.Price = price;
        product.CampaignPrice = campaign;
        product.CampaignEndsAt = endsAt;
        product.IsActive = active;
        product.CreatedAt = Now.AddDays(-ageDays);
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<(int Root, int Child)> CategoriesAsync(HerYerdeContext context, string childSlug = "tencere-tava")
    {
        var root = new Category { Name = "Ev", Slug = "ev", SortOrder = 1, IsActive = true };
        context.Categories.Add(root);
        await context.SaveChangesAsync();
        var child = new Category { Name = "Alt", Slug = childSlug, ParentId = root.Id, SortOrder = 1, IsActive = true };
        context.Categories.Add(child);
        await context.SaveChangesAsync();
        return (root.Id, child.Id);
    }

    [Fact]
    public async Task Fiyat_siralamasi_kampanyali_fiyati_esas_alir()
    {
        await using var context = TestDb.NewContext();
        var (_, child) = await CategoriesAsync(context);
        var pahali = await AddAsync(context, child, "pahali", 900m);
        var kampanyali = await AddAsync(context, child, "kampanyali", 1000m, 500m, Now.AddDays(1));
        var ucuz = await AddAsync(context, child, "ucuz", 700m);

        var dal = new EfProductDal(context);
        var (items, _) = await dal.GetActiveAsync(new ProductQuery { Order = ProductOrder.Price, Now = Now });

        Assert.Equal([kampanyali, ucuz, pahali], items.Select(i => i.Product.Id));
    }

    [Fact]
    public async Task Suresi_dolmus_kampanya_fiyat_siralamasinda_liste_fiyatiyla_gecer()
    {
        await using var context = TestDb.NewContext();
        var (_, child) = await CategoriesAsync(context);
        var dolmus = await AddAsync(context, child, "dolmus", 900m, 100m, Now.AddSeconds(-1));
        var ucuz = await AddAsync(context, child, "ucuz", 700m);

        var dal = new EfProductDal(context);
        var (items, _) = await dal.GetActiveAsync(new ProductQuery { Order = ProductOrder.Price, Now = Now });

        Assert.Equal([ucuz, dolmus], items.Select(i => i.Product.Id));
    }

    [Fact]
    public async Task Hero_gecmis_kampanyayi_atlar_en_yakin_biteni_secer_suresizler_sona_kalir()
    {
        await using var context = TestDb.NewContext();
        var (_, child) = await CategoriesAsync(context);
        await AddAsync(context, child, "gecmis", 1000m, 800m, Now.AddDays(-1));
        await AddAsync(context, child, "uzak", 1000m, 800m, Now.AddDays(5));
        var yakin = await AddAsync(context, child, "yakin", 1000m, 800m, Now.AddDays(2));
        await AddAsync(context, child, "suresiz", 1000m, 800m);

        var hero = await new EfProductDal(context).GetCampaignHeroAsync(Now);

        Assert.NotNull(hero);
        Assert.Equal(yakin, hero.Value.Product.Id);
    }

    [Fact]
    public async Task Benzer_urunler_ayni_alt_kategoriden_kendisi_haric_en_fazla_dort()
    {
        await using var context = TestDb.NewContext();
        var (_, child) = await CategoriesAsync(context);
        var baska = new Category { Name = "Başka", Slug = "yemek-takimi", ParentId = null, SortOrder = 2, IsActive = true };
        context.Categories.Add(baska);
        await context.SaveChangesAsync();

        var self = await AddAsync(context, child, "kendisi", 100m);
        var digerKategori = await AddAsync(context, baska.Id, "diger-kategori", 100m);
        var taslak = await AddAsync(context, child, "taslak", 100m, active: false);
        for (var i = 0; i < 5; i++)
        {
            await AddAsync(context, child, "benzer-" + i, 100m);
        }

        var (items, _) = await new EfProductDal(context).GetActiveAsync(new ProductQuery
        {
            CategoryIds = [child],
            ExcludedProductId = self,
            Now = Now,
            Take = 4
        });

        var ids = items.Select(i => i.Product.Id).ToList();
        Assert.Equal(4, ids.Count);
        Assert.DoesNotContain(self, ids);
        Assert.DoesNotContain(digerKategori, ids);
        Assert.DoesNotContain(taslak, ids);
    }

    [Fact]
    public async Task Sayfalama_toplam_sayiyi_ayni_sorguda_dondurur()
    {
        await using var context = TestDb.NewContext();
        var (_, child) = await CategoriesAsync(context);
        for (var i = 0; i < 30; i++)
        {
            await AddAsync(context, child, "urun-" + i, 100m + i, ageDays: i);
        }

        var (items, total) = await new EfProductDal(context).GetActiveAsync(new ProductQuery
        {
            CategoryIds = [child],
            Now = Now,
            Skip = 24,
            Take = 24
        });

        Assert.Equal(30, total);
        Assert.Equal(6, items.Count);
    }
}
