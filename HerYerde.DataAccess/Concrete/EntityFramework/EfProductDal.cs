using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfProductDal : EfEntityRepositoryBase<Product, HerYerdeContext>, IProductDal
{
    public EfProductDal(HerYerdeContext context) : base(context)
    {
    }

    public async Task<List<ProductSkuRow>> ProductsBySkuAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default)
    {
        if (skus.Count == 0)
        {
            return [];
        }

        // Varyantsız üründe satırın kodu ürünün slug'ı, varyantlıda varyantın stok kodudur.
        var direct = await Context.Products.AsNoTracking()
            .Where(p => p.IsActive && skus.Contains(p.Slug))
            .Select(p => new ProductSkuRow(p.Slug, p.Slug, p.Name))
            .ToListAsync(cancellationToken);

        var byVariant = await Context.ProductVariants.AsNoTracking()
            .Where(v => skus.Contains(v.Sku))
            .Join(Context.Products.Where(p => p.IsActive), v => v.ProductId, p => p.Id, (v, p) => new ProductSkuRow(v.Sku, p.Slug, p.Name))
            .ToListAsync(cancellationToken);

        return [.. direct, .. byVariant];
    }

    public async Task<(List<ProductRow> Items, int Total)> GetActiveAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = Scope(query, withSelections: true);

        var ordered = query.Order switch
        {
            ProductOrder.Price => source
                .OrderBy(r => r.Effective)
                .ThenBy(r => r.Product.Id),
            ProductOrder.Featured => source
                .OrderBy(r => r.Product.FeaturedOrder)
                .ThenByDescending(r => r.Product.CreatedAt)
                .ThenByDescending(r => r.Product.Id),
            _ => source
                .OrderByDescending(r => r.Product.CreatedAt)
                .ThenByDescending(r => r.Product.Id)
        };

        // Toplam sayı, sayfa satırlarının yanında alt sorgu olarak gelir: sayfalama tek gidiş-dönüş.
        var rows = await ordered
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(r => new
            {
                r.Product,
                r.Slug,
                // Ev'de ürünün kendi stoğu, Giyim'de varyantların tamamı bitmişse tükendi.
                SoldOut = r.Product.Stock == 0
                          || (r.Product.Stock == null
                              && Context.ProductVariants.Any(v => v.ProductId == r.Product.Id)
                              && !Context.ProductVariants.Any(v => v.ProductId == r.Product.Id && v.Stock > 0)),
                // Kart önizlemesi ayrı gidiş-dönüş olmasın: videosu olan üründe ilk videonun webm'i.
                PreviewUrl = Context.ProductVideos
                    .Where(v => v.ProductId == r.Product.Id)
                    .OrderBy(v => v.SortOrder)
                    .Select(v => v.PreviewUrl)
                    .FirstOrDefault(),
                Total = source.Count()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new ProductRow(r.Product, r.Slug, r.SoldOut, r.PreviewUrl)).ToList();
        return (items, rows.Count == 0 ? 0 : rows[0].Total);
    }

    public async Task<List<FacetRow>> GetFacetsAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        var scope = Scope(query, withSelections: false);
        var ids = scope.Select(r => r.Product.Id);

        var brands = Context.Products
            .Where(p => ids.Contains(p.Id) && p.BrandId != null)
            .Join(Context.Brands, p => p.BrandId, b => (int?)b.Id, (p, b) => new { b.Name, b.Slug })
            .GroupBy(x => new { x.Name, x.Slug })
            .Select(g => new FacetRow { Kind = FacetRow.BrandKind, First = g.Key.Name, Second = g.Key.Slug, Count = g.Count() });

        var attributes = Context.ProductAttributes
            .Where(a => ids.Contains(a.ProductId))
            .GroupBy(a => new { a.Name, a.Value })
            .Select(g => new FacetRow { Kind = FacetRow.AttributeKind, First = g.Key.Name, Second = g.Key.Value, Count = g.Count() });

        // Stokta olanlar ve kampanyalı seçeneklerinin sayısı; grup boşsa satır gelmez, sayı 0 sayılır.
        var inStock = InStock(scope)
            .GroupBy(_ => 1)
            .Select(g => new FacetRow { Kind = FacetRow.InStockKind, First = "", Second = "", Count = g.Count() });
        var campaign = Campaign(scope, query.Now)
            .GroupBy(_ => 1)
            .Select(g => new FacetRow { Kind = FacetRow.CampaignKind, First = "", Second = "", Count = g.Count() });

        return await brands.Concat(attributes).Concat(inStock).Concat(campaign).ToListAsync(cancellationToken);
    }

    /// <summary>Kapsam (kategori, marka sayfası, terim, fiyat …) her zaman; ziyaretçinin marka/özellik/stok/kampanya seçimleri
    /// yalnız istenirse. Süzgeç paneli sayıları seçimsiz kapsamdan gelir.</summary>
    private IQueryable<ListingRow> Scope(ProductQuery query, bool withSelections)
    {
        var source = Context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Join(Context.Categories, p => p.CategoryId, c => c.Id, (p, c) => new ListingRow
            {
                Product = p,
                Slug = c.Slug,
                CategoryName = c.Name,
                // Etkin fiyat: kampanya süresi içindeyse kampanya fiyatı. Süzme de sıralama da buna bakar.
                Effective = p.CampaignPrice != null
                            && p.CampaignPrice < p.Price
                            && (p.CampaignEndsAt == null || p.CampaignEndsAt > query.Now)
                    ? p.CampaignPrice!.Value
                    : p.Price
            });

        if (query.CategoryIds.Count > 0)
        {
            source = source.Where(r => query.CategoryIds.Contains(r.Product.CategoryId));
        }

        if (query.BrandIds.Count > 0)
        {
            source = source.Where(r => r.Product.BrandId != null && query.BrandIds.Contains(r.Product.BrandId.Value));
        }

        if (query.ExcludedProductId is { } excluded)
        {
            source = source.Where(r => r.Product.Id != excluded);
        }

        if (query.FeaturedOnly)
        {
            source = source.Where(r => r.Product.IsFeatured);
        }

        if (LikePattern(query.Term) is { } pattern)
        {
            source = source.Where(r => EF.Functions.Like(r.Product.Name, pattern, LikeEscape)
                                       || EF.Functions.Like(r.Product.Description, pattern, LikeEscape)
                                       || EF.Functions.Like(r.CategoryName, pattern, LikeEscape));
        }

        foreach (var word in query.SearchWords)
        {
            var patterns = word.Select(LikePattern).OfType<string>().ToList();
            source = source.Where(r => patterns.Any(p => EF.Functions.Like(r.Product.Name, p, LikeEscape)
                                                         || EF.Functions.Like(r.Product.Description, p, LikeEscape)
                                                         || EF.Functions.Like(r.CategoryName, p, LikeEscape)));
        }

        if (query.MinPrice is { } min)
        {
            source = source.Where(r => r.Effective >= min);
        }

        if (query.MaxPrice is { } max)
        {
            source = source.Where(r => r.Effective <= max);
        }

        if (!withSelections)
        {
            return source;
        }

        if (query.BrandSlugs.Count > 0)
        {
            var brandIds = Context.Brands.Where(b => query.BrandSlugs.Contains(b.Slug)).Select(b => (int?)b.Id);
            source = source.Where(r => brandIds.Contains(r.Product.BrandId));
        }

        foreach (var attribute in query.Attributes)
        {
            var name = attribute.Name;
            var values = attribute.Values;
            source = source.Where(r => Context.ProductAttributes.Any(a => a.ProductId == r.Product.Id && a.Name == name && values.Contains(a.Value)));
        }

        if (query.InStockOnly)
        {
            source = InStock(source);
        }

        if (query.CampaignOnly)
        {
            source = Campaign(source, query.Now);
        }

        return source;
    }

    /// <summary>Tükenmişler dışarıda: stoğu 0 olan ürün ve bütün varyantları bitmiş ürün.</summary>
    private IQueryable<ListingRow> InStock(IQueryable<ListingRow> source)
        => source.Where(r => r.Product.Stock > 0
                             || (r.Product.Stock == null
                                 && (!Context.ProductVariants.Any(v => v.ProductId == r.Product.Id)
                                     || Context.ProductVariants.Any(v => v.ProductId == r.Product.Id && v.Stock > 0))));

    private static IQueryable<ListingRow> Campaign(IQueryable<ListingRow> source, DateTime now)
        => source.Where(r => r.Product.CampaignPrice != null
                             && r.Product.CampaignPrice < r.Product.Price
                             && (r.Product.CampaignEndsAt == null || r.Product.CampaignEndsAt > now));

    /// <summary>Listeleme satırı; nesne başlatıcıyla kurulduğu için üzerindeki süzme ve sıralama SQL'e çevrilir.</summary>
    private sealed class ListingRow
    {
        public Product Product { get; init; } = null!;
        public string Slug { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public decimal Effective { get; init; }
    }

    public async Task<(Product Product, string CategorySlug)?> GetCampaignHeroAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var row = await Context.Products
            .AsNoTracking()
            .Where(p => p.IsActive
                        && p.CampaignPrice != null
                        && p.CampaignPrice < p.Price
                        && (p.CampaignEndsAt == null || p.CampaignEndsAt > now))
            .Join(Context.Categories, p => p.CategoryId, c => c.Id, (p, c) => new { Product = p, c.Slug })
            // Süresizler en sona: NULL bitiş en büyük sayılır.
            .OrderBy(r => r.Product.CampaignEndsAt == null ? 1 : 0)
            .ThenBy(r => r.Product.CampaignEndsAt)
            .ThenBy(r => r.Product.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : (row.Product, row.Slug);
    }

    public async Task<Dictionary<int, int>> GetStockTotalsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Context.ProductVariants
            .AsNoTracking()
            .GroupBy(v => v.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(v => v.Stock) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ProductId, r => r.Stock);
    }

    public async Task<List<LowStockRow>> GetLowStockAsync(int threshold, CancellationToken cancellationToken = default)
    {
        var products = await Context.Products
            .AsNoTracking()
            .Where(p => p.Stock != null && p.Stock <= threshold)
            .Select(p => new LowStockRow(p.Id, p.Name, null, null, null, p.Stock!.Value))
            .ToListAsync(cancellationToken);

        var variants = await Context.ProductVariants
            .AsNoTracking()
            .Where(v => v.Stock <= threshold)
            .Join(Context.Products, v => v.ProductId, p => p.Id, (v, p) => new LowStockRow(p.Id, p.Name, v.Sku, v.Size, v.Color, v.Stock))
            .ToListAsync(cancellationToken);

        return products.Concat(variants).OrderBy(r => r.Stock).ThenBy(r => r.ProductName).ToList();
    }

    public async Task<(Product Product, List<ProductVariant> Variants, List<ProductAttribute> Attributes)?> GetActiveWithVariantsBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var row = await Context.Products
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.IsActive)
            .Select(p => new
            {
                Product = p,
                Variants = Context.ProductVariants.Where(v => v.ProductId == p.Id).ToList(),
                Attributes = Context.ProductAttributes.Where(a => a.ProductId == p.Id).OrderBy(a => a.SortOrder).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : (row.Product, row.Variants, row.Attributes);
    }

    public Task<int> CountLowStockAsync(int threshold, CancellationToken cancellationToken = default)
        => Context.Database
            .SqlQuery<int>($"""
                SELECT (SELECT COUNT(*) FROM product WHERE deleted_at IS NULL AND stock IS NOT NULL AND stock <= {threshold})
                     + (SELECT COUNT(*) FROM product_variant v JOIN product p ON p.id = v.product_id WHERE p.deleted_at IS NULL AND v.stock <= {threshold}) AS [Value]
                """)
            .SingleAsync(cancellationToken);

    public Task<bool> SlugTakenAsync(string slug, int excludedId, CancellationToken cancellationToken = default)
        => Context.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(p => p.Slug == slug && p.Id != excludedId, cancellationToken);

    public Task<int> TryDecrementStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
        => Context.Products
            .Where(p => p.Id == productId && p.Stock != null && p.Stock >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity), cancellationToken);

    public Task<int> IncrementStockBySlugAsync(string slug, int quantity, CancellationToken cancellationToken = default)
        => Context.Products
            .Where(p => p.Slug == slug && p.Stock != null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + quantity), cancellationToken);

    private const string LikeEscape = "\\";

    /// <summary>Kullanıcının yazdığı %, _ ve [ karakterleri joker sayılmaz.</summary>
    private static string? LikePattern(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var escaped = term.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);

        return "%" + escaped + "%";
    }
}
