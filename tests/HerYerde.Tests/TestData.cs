using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
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
    public const decimal FreeShippingOver = 2500m;
    public const int MaxQtyPerLine = 10;
    public const string Iban = "TR00 0000 0000 0000 0000 0000 00";
    public const string StoreEmail = "magaza@heryerde.test";
    public const string Carrier = "Yurtiçi";
    public const string TrackingUrlTemplate = "https://kargo.test/takip/{0}";
    public const string BaseUrl = "https://heryerde.test";

    public static ShopSettings NewShopSettings(decimal? freeShippingOver = null) => new()
    {
        ShippingFee = ShippingFee,
        FreeShippingOver = freeShippingOver ?? FreeShippingOver,
        Iban = Iban,
        MaxQtyPerLine = MaxQtyPerLine,
        BaseUrl = BaseUrl,
        ReturnAddress = ReturnAddress,
        ReturnCarrier = ReturnCarrier
    };

    public static NotificationSettings NewNotificationSettings() => new()
    {
        From = "siparis@heryerde.test",
        StoreTo = StoreEmail
    };

    public static ShippingSettings NewShippingSettings() => new()
    {
        Carriers = [new CarrierSettings { Name = Carrier, TrackingUrl = TrackingUrlTemplate }]
    };

    public static NotificationManager NewNotificationManager(
        HerYerdeContext context,
        INotificationSender? sender = null,
        TimeProvider? clock = null) => new(
        new EfOutboxMessageDal(context),
        new EfOrderDal(context),
        new EfOrderItemDal(context),
        new EfProductDal(context),
        sender ?? new FakeNotificationSender(),
        new EfUnitOfWork(context),
        Options.Create(NewNotificationSettings()),
        Options.Create(NewShippingSettings()),
        Options.Create(NewShopSettings()),
        clock ?? TestClock.Fixed);

    public static ContactManager NewContactManager(HerYerdeContext context) => new(
        new EfContactMessageDal(context),
        NewNotificationManager(context),
        new EfUnitOfWork(context),
        TestClock.Fixed);

    public static SearchLogManager NewSearchLogManager(HerYerdeContext context) => new(
        new EfSearchLogDal(context),
        new EfUnitOfWork(context),
        TestClock.Fixed);

    public static CouponManager NewCouponManager(HerYerdeContext context, TimeProvider? clock = null) => new(
        new EfCouponDal(context),
        new EfCartDal(context),
        new EfOrderDal(context),
        new EfUnitOfWork(context),
        clock ?? TestClock.Fixed);

    public static CartManager NewCartManager(
        HerYerdeContext context,
        TimeProvider? clock = null,
        decimal? freeShippingOver = null) => new(
        new EfCartDal(context),
        new EfCartItemDal(context),
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfProductImageDal(context),
        new EfGiftRegistryDal(context),
        new EfGiftRegistryItemDal(context),
        new EfUnitOfWork(context),
        NewCouponManager(context, clock),
        Options.Create(NewShopSettings(freeShippingOver)),
        clock ?? TestClock.Fixed);

    public static AdminAuthManager NewAdminAuthManager(HerYerdeContext context) => new(
        new EfAdminUserDal(context),
        new EfUnitOfWork(context),
        NewNotificationManager(context),
        TestClock.Fixed);

    public static ProductManager NewProductManager(HerYerdeContext context) => new(
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfProductImageDal(context),
        new EfProductVideoDal(context),
        new EfCategoryDal(context),
        new EfSlugHistoryDal(context),
        new EfUnitOfWork(context));

    public static OrderManager NewOrderManager(
        HerYerdeContext context,
        TimeProvider? clock = null,
        decimal? freeShippingOver = null) => new(
        new EfOrderDal(context),
        new EfOrderItemDal(context),
        new EfCartItemDal(context),
        new EfCartDal(context),
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfUnitOfWork(context),
        NewNotificationManager(context, clock: clock),
        new EfPaymentDal(context),
        new EfPaymentNoticeDal(context),
        new EfReturnRequestDal(context),
        new EfOrderNoteDal(context),
        NewCouponManager(context, clock),
        NewGiftRegistryManager(context, clock),
        Options.Create(NewShopSettings(freeShippingOver)),
        Options.Create(NewShippingSettings()),
        clock ?? TestClock.Fixed);

    public static GiftRegistryManager NewGiftRegistryManager(HerYerdeContext context, TimeProvider? clock = null) => new(
        new EfGiftRegistryDal(context),
        new EfGiftRegistryItemDal(context),
        new EfCartItemDal(context),
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfProductImageDal(context),
        NewNotificationManager(context, clock: clock),
        new EfUnitOfWork(context),
        clock ?? TestClock.Fixed);

    public const string ReturnAddress = "HerYerde İade, Atatürk Cad. 5, Kadıköy/İstanbul";
    public const string ReturnCarrier = "Yurtiçi Kargo, anlaşma kodu 123456";

    public static PaymentManager NewPaymentManager(HerYerdeContext context, IPaymentProvider provider, TimeProvider? clock = null) => new(
        new EfPaymentDal(context),
        new EfOrderDal(context),
        new EfOrderItemDal(context),
        new EfCartItemDal(context),
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfUnitOfWork(context),
        NewNotificationManager(context, clock: clock),
        provider,
        NewGiftRegistryManager(context, clock),
        Options.Create(NewShopSettings()),
        clock ?? TestClock.Fixed);

    public static ReturnManager NewReturnManager(HerYerdeContext context, IPaymentProvider provider, TimeProvider? clock = null) => new(
        new EfReturnRequestDal(context),
        new EfReturnRequestItemDal(context),
        new EfOrderDal(context),
        new EfOrderItemDal(context),
        new EfProductDal(context),
        new EfProductVariantDal(context),
        new EfPaymentDal(context),
        provider,
        NewPaymentManager(context, provider, clock),
        NewOrderManager(context, clock),
        NewNotificationManager(context, clock: clock),
        new EfUnitOfWork(context),
        clock ?? TestClock.Fixed);

    public static KvkkManager NewKvkkManager(HerYerdeContext context, TimeProvider? clock = null) => new(
        new EfOrderDal(context),
        new EfOrderItemDal(context),
        new EfPaymentDal(context),
        new EfPaymentNoticeDal(context),
        new EfReturnRequestDal(context),
        new EfReturnRequestItemDal(context),
        new EfContactMessageDal(context),
        new EfProductReviewDal(context),
        new EfKvkkRequestDal(context),
        NewOrderManager(context, clock),
        new EfUnitOfWork(context),
        clock ?? TestClock.Fixed);

    /// <summary>Ev alanında varyantsız ürün.</summary>
    public static async Task<int> AddHomeProductAsync(
        HerYerdeContext context,
        string name,
        string slug,
        decimal price = 450m,
        decimal? campaignPrice = null,
        int? stock = null)
    {
        var categoryId = await RootCategoryIdAsync(context, "Ev", "ev");
        var product = NewProduct(categoryId, name, slug);
        product.Price = price;
        product.CampaignPrice = campaignPrice;
        product.Stock = stock;
        await new EfProductDal(context).AddAsync(product);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return product.Id;
    }

    /// <summary>Ürüne "1 alana 1 hediye" kampanyası yazar; bitiş boşsa süresizdir.</summary>
    public static async Task SetGiftAsync(
        HerYerdeContext context,
        int productId,
        GiftMode mode,
        int? giftProductId = null,
        int giftQty = 1,
        DateTime? endsAt = null)
    {
        var dal = new EfProductDal(context);
        var product = (await dal.GetTrackedAsync(p => p.Id == productId))!;
        product.GiftMode = mode;
        product.GiftProductId = giftProductId;
        product.GiftQty = giftQty;
        product.CampaignEndsAt = endsAt;
        await new EfUnitOfWork(context).SaveChangesAsync();
    }

    public static async Task<int?> ProductStockAsync(HerYerdeContext context, int productId)
        => (await new EfProductDal(context).GetAsync(p => p.Id == productId))!.Stock;

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

    /// <summary>Ev kökünün altında alt kategori; kök yoksa açılır.</summary>
    public static async Task<int> AddChildCategoryAsync(HerYerdeContext context, string name, string slug)
    {
        var category = new Category
        {
            Name = name,
            Slug = slug,
            ParentId = await RootCategoryIdAsync(context, "Ev", "ev"),
            SortOrder = 1,
            IsActive = true
        };
        await new EfCategoryDal(context).AddAsync(category);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return category.Id;
    }

    public static async Task AddImageAsync(HerYerdeContext context, int productId, string url)
    {
        await new EfProductImageDal(context).AddAsync(new ProductImage
        {
            ProductId = productId,
            Url = url,
            Alt = "Ürün görseli",
            IsPrimary = true
        });
        await new EfUnitOfWork(context).SaveChangesAsync();
    }

    /// <summary>Ürüne yüklenmiş gibi bir video satırı yazar; 720p adresini döner (poster/önizleme aynı addan türer).</summary>
    public static async Task<string> AddVideoAsync(HerYerdeContext context, int productId, int duration = 3)
    {
        var name = Guid.NewGuid().ToString("n");
        var url = $"/uploads/videos/{productId}/{name}-720.mp4";
        await new EfProductVideoDal(context).AddAsync(new ProductVideo
        {
            ProductId = productId,
            Url = url,
            PosterUrl = $"/uploads/videos/{productId}/{name}-poster.jpg",
            PreviewUrl = $"/uploads/videos/{productId}/{name}-onizleme.webm",
            Duration = duration,
            CreatedAt = TestClock.Fixed.GetUtcNow().UtcDateTime
        });
        await new EfUnitOfWork(context).SaveChangesAsync();
        return url;
    }

    public static async Task<int> AddBrandAsync(HerYerdeContext context, string name, string slug)
    {
        var brand = new Brand { Name = name, Slug = slug };
        await new EfBrandDal(context).AddAsync(brand);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return brand.Id;
    }

    public static async Task AddAttributeAsync(HerYerdeContext context, int productId, string name, string value, int sortOrder = 0)
    {
        await new EfProductAttributeDal(context).AddAsync(new ProductAttribute { ProductId = productId, Name = name, Value = value, SortOrder = sortOrder });
        await new EfUnitOfWork(context).SaveChangesAsync();
    }

    public static async Task SetBrandAsync(HerYerdeContext context, int productId, int brandId)
    {
        var product = (await new EfProductDal(context).GetTrackedAsync(p => p.Id == productId))!;
        product.BrandId = brandId;
        await new EfUnitOfWork(context).SaveChangesAsync();
    }

    /// <summary>Çeyiz listesi; paylaşılan adres ve yönetim anahtarıyla döner.</summary>
    public static async Task<(int Id, string Slug, Guid Token)> AddGiftRegistryAsync(
        HerYerdeContext context,
        string phone = "05321112233",
        string? email = "zeynep@ornek.test",
        bool isPublic = true,
        DateTime? eventDate = null)
    {
        var registry = new GiftRegistry
        {
            Slug = Guid.NewGuid().ToString("n")[..10],
            ManageToken = Guid.NewGuid(),
            OwnerName = "Zeynep Kaya",
            Phone = phone,
            Email = email,
            EventDate = eventDate ?? new DateTime(2026, 11, 14),
            Message = "Yeni evimiz için küçük bir liste.",
            IsPublic = isPublic,
            CreatedAt = TestClock.Fixed.GetUtcNow().UtcDateTime
        };
        await new EfGiftRegistryDal(context).AddAsync(registry);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return (registry.Id, registry.Slug, registry.ManageToken);
    }

    public static async Task<int> AddGiftRegistryItemAsync(
        HerYerdeContext context,
        int registryId,
        int productId,
        int desired = 3,
        int received = 0,
        int? variantId = null)
    {
        var item = new GiftRegistryItem
        {
            GiftRegistryId = registryId,
            ProductId = productId,
            VariantId = variantId,
            DesiredQty = desired,
            ReceivedQty = received
        };
        await new EfGiftRegistryItemDal(context).AddAsync(item);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return item.Id;
    }

    public static async Task SetDescriptionAsync(HerYerdeContext context, int productId, string description)
    {
        var product = (await new EfProductDal(context).GetTrackedAsync(p => p.Id == productId))!;
        product.Description = description;
        await new EfUnitOfWork(context).SaveChangesAsync();
    }

    private static async Task<int> RootCategoryIdAsync(HerYerdeContext context, string name, string slug)
    {
        var dal = new EfCategoryDal(context);
        var existing = await dal.GetAsync(c => c.Slug == slug);
        return existing?.Id ?? await AddRootCategoryAsync(context, name, slug);
    }
}
