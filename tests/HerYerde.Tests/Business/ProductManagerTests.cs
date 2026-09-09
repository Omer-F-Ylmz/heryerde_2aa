using System.Net;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class ProductManagerTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static ProductManager NewManager(HerYerdeContext context) => new(
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfProductImageDal(context),
        new EfCategoryDal(context),
        new EfUnitOfWork(context));

    [Fact]
    public async Task Ev_urunune_bedenli_varyant_eklenmez()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        var productId = await NewProductAsync(context, manager, "ev", "Çelik Tencere");

        var (status, result) = await manager.AddVariantAsync(new ProductVariant
        {
            ProductId = productId,
            Size = "M",
            Sku = "TNC-M",
            Stock = 4
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("beden", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await new EfProductVariantDal(context).GetListAsync(v => v.ProductId == productId));
    }

    [Fact]
    public async Task Ev_urunune_bedensiz_renksiz_varyant_eklenir()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        var productId = await NewProductAsync(context, manager, "ev", "Çelik Tencere");

        var (status, _) = await manager.AddVariantAsync(new ProductVariant { ProductId = productId, Sku = "TNC-24", Stock = 4 });

        Assert.Equal(HttpStatusCode.Created, status);
    }

    [Fact]
    public async Task Giyim_urunu_varyantsiz_yayina_alinamaz()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        var productId = await NewProductAsync(context, manager, "giyim", "Şile Bezi Şalvar");

        var (status, result) = await manager.UpdateAsync(new Product
        {
            Id = productId,
            Name = "Şile Bezi Şalvar",
            Description = "Pamuklu.",
            CategoryId = await CategoryIdAsync(context, "giyim"),
            Price = 450m,
            IsActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("varyant", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False((await new EfProductDal(context).GetAsync(p => p.Id == productId))!.IsActive);
    }

    [Fact]
    public async Task Giyim_urunu_varyant_eklenince_yayina_alinir()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        var categoryId = await CategoryIdAsync(context, "giyim");
        var productId = await NewProductAsync(context, manager, "giyim", "Şile Bezi Şalvar");
        await manager.AddVariantAsync(new ProductVariant { ProductId = productId, Size = "M", Color = "Kiremit", Sku = "SLV-M-KRM", Stock = 6 });

        var (status, _) = await manager.UpdateAsync(new Product
        {
            Id = productId,
            Name = "Şile Bezi Şalvar",
            Description = "Pamuklu.",
            CategoryId = categoryId,
            Price = 450m,
            IsActive = true
        });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True((await new EfProductDal(context).GetAsync(p => p.Id == productId))!.IsActive);
    }

    [Fact]
    public async Task Ayni_stok_kodu_ikinci_varyantta_409_doner()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        var productId = await NewProductAsync(context, manager, "giyim", "Şile Bezi Şalvar");
        await manager.AddVariantAsync(new ProductVariant { ProductId = productId, Size = "M", Color = "Kiremit", Sku = "SLV-M-KRM", Stock = 6 });

        var (status, _) = await manager.AddVariantAsync(new ProductVariant { ProductId = productId, Size = "L", Color = "Kiremit", Sku = "SLV-M-KRM", Stock = 2 });

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task Silinen_urun_listeden_duser_ama_kayit_soft_delete_ile_kalir()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        var productId = await NewProductAsync(context, manager, "ev", "Çelik Tencere");

        var (status, _) = await manager.DeleteAsync(productId);

        var (_, all) = await manager.GetAllAsync();
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(all.Data!);
        Assert.NotNull(await context.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == productId));
    }

    [Fact]
    public async Task Yeni_birincil_gorsel_isaretlenince_onceki_birincillik_biter()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        var productId = await NewProductAsync(context, manager, "ev", "Çelik Tencere");
        await manager.AddImageAsync(new ProductImage { ProductId = productId, Url = "/img/1.jpg", Alt = "Tencere önden", SortOrder = 0, IsPrimary = true });
        await manager.AddImageAsync(new ProductImage { ProductId = productId, Url = "/img/2.jpg", Alt = "Tencere yandan", SortOrder = 1 });

        var (_, images) = await manager.GetImagesAsync(productId);
        var second = images.Data!.Single(i => i.Url == "/img/2.jpg");
        await manager.SetPrimaryImageAsync(second.Id);

        var (_, updated) = await manager.GetImagesAsync(productId);
        Assert.Single(updated.Data!, i => i.IsPrimary);
        Assert.True(updated.Data!.Single(i => i.Url == "/img/2.jpg").IsPrimary);
    }

    [Fact]
    public async Task GetByIdAsync_izlemeyen_kayit_dondurur_UpdateAsync_ise_yazar()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        var categoryId = await CategoryIdAsync(context, "ev");
        var productId = await NewProductAsync(context, manager, "ev", "Çelik Tencere");

        var (_, fetched) = await manager.GetByIdAsync(productId);
        fetched.Data!.Price = 9999m;
        await new EfUnitOfWork(context).SaveChangesAsync();

        Assert.Equal(450m, (await new EfProductDal(context).GetAsync(p => p.Id == productId))!.Price);

        await manager.UpdateAsync(new Product
        {
            Id = productId,
            Name = "Çelik Tencere",
            Description = "24 cm.",
            CategoryId = categoryId,
            Price = 1290.50m,
            IsActive = false
        });

        Assert.Equal(1290.50m, (await new EfProductDal(context).GetAsync(p => p.Id == productId))!.Price);
    }

    private static async Task<int> CategoryIdAsync(HerYerdeContext context, string rootSlug)
    {
        var dal = new EfCategoryDal(context);
        var existing = await dal.GetAsync(c => c.Slug == rootSlug);
        if (existing is not null)
        {
            return existing.Id;
        }

        var name = rootSlug == "giyim" ? "Giyim" : "Ev";
        return await TestData.AddRootCategoryAsync(context, name, rootSlug);
    }

    private static async Task<int> NewProductAsync(HerYerdeContext context, ProductManager manager, string rootSlug, string name)
    {
        var categoryId = await CategoryIdAsync(context, rootSlug);
        var (_, created) = await manager.AddAsync(new Product
        {
            Name = name,
            Description = "Pamuklu, beli lastikli.",
            CategoryId = categoryId,
            Price = 450m,
            IsActive = false
        });

        return created.Data!.Id;
    }
}
