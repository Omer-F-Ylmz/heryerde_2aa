using HerYerde.Business;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.Extensions.Options;

namespace HerYerde.Tests;

public static class TestData
{
    public static async Task<int> AddRootCategoryAsync(HerYerdeContext context, string name = "Giyim", string slug = "giyim")
    {
        var category = new Category { Name = name, Slug = slug, SortOrder = 1, IsActive = true };
        await new EfCategoryDal(context).AddAsync(category);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return category.Id;
    }

    public static async Task<int> AddProductAsync(HerYerdeContext context, int categoryId, string name, string slug)
    {
        var product = NewProduct(categoryId, name, slug);
        await new EfProductDal(context).AddAsync(product);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return product.Id;
    }

    public static Product NewProduct(int categoryId, string name, string slug) => new()
    {
        Name = name,
        Slug = slug,
        Description = "Pamuklu, beli lastikli.",
        CategoryId = categoryId,
        Price = 450m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public const decimal ShippingFee = 79.90m;
    public const int MaxQtyPerLine = 10;
    public const string Iban = "TR00 0000 0000 0000 0000 0000 00";

    public static CartManager NewCartManager(HerYerdeContext context, TimeProvider? clock = null) => new(
        new EfCartDal(context),
        new EfCartItemDal(context),
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfProductImageDal(context),
        new EfUnitOfWork(context),
        Options.Create(new ShopSettings { ShippingFee = ShippingFee, Iban = Iban, MaxQtyPerLine = MaxQtyPerLine }),
        clock ?? TestClock.Fixed);

    public static AdminAuthManager NewAdminAuthManager(HerYerdeContext context) => new(
        new EfAdminUserDal(context),
        new EfUnitOfWork(context));

    public static ProductManager NewProductManager(HerYerdeContext context) => new(
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfProductImageDal(context),
        new EfCategoryDal(context),
        new EfUnitOfWork(context));

    public static OrderManager NewOrderManager(HerYerdeContext context, TimeProvider? clock = null) => new(
        new EfOrderDal(context),
        new EfOrderItemDal(context),
        new EfCartItemDal(context),
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfUnitOfWork(context),
        Options.Create(new ShopSettings { ShippingFee = ShippingFee, Iban = Iban }),
        clock ?? TestClock.Fixed);

    /// <summary>Ev alanında varyantsız ürün.</summary>
    public static async Task<int> AddHomeProductAsync(
        HerYerdeContext context,
        string name,
        string slug,
        decimal price = 450m,
        decimal? campaignPrice = null)
    {
        var categoryId = await RootCategoryIdAsync(context, "Ev", "ev");
        var product = NewProduct(categoryId, name, slug);
        product.Price = price;
        product.CampaignPrice = campaignPrice;
        await new EfProductDal(context).AddAsync(product);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return product.Id;
    }

    /// <summary>Giyim alanında tek varyantlı ürün; varyant kimliğiyle döner.</summary>
    public static async Task<(int ProductId, int VariantId)> AddClothingProductAsync(
        HerYerdeContext context,
        string name,
        string slug,
        string size = "M",
        string color = "Kiremit",
        int stock = 5,
        decimal price = 450m)
    {
        var categoryId = await RootCategoryIdAsync(context, "Giyim", "giyim");
        var product = NewProduct(categoryId, name, slug);
        product.Price = price;
        await new EfProductDal(context).AddAsync(product);
        await new EfUnitOfWork(context).SaveChangesAsync();

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Size = size,
            Color = color,
            Sku = slug.ToUpperInvariant() + "-" + size,
            Stock = stock
        };
        await new EfProductVariantDal(context).AddAsync(variant);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return (product.Id, variant.Id);
    }

    private static async Task<int> RootCategoryIdAsync(HerYerdeContext context, string name, string slug)
    {
        var dal = new EfCategoryDal(context);
        var existing = await dal.GetAsync(c => c.Slug == slug);
        return existing?.Id ?? await AddRootCategoryAsync(context, name, slug);
    }
}
