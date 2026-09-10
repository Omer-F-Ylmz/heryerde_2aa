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

    public async Task<(List<ProductRow> Items, int Total)> GetActiveAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = Context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Join(Context.Categories, p => p.CategoryId, c => c.Id, (p, c) => new
            {
                Product = p,
                c.Slug,
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

        if (query.ExcludedProductId is { } excluded)
        {
            source = source.Where(r => r.Product.Id != excluded);
        }

        if (LikePattern(query.Term) is { } pattern)
        {
            source = source.Where(r => EF.Functions.Like(r.Product.Name, pattern, LikeEscape)
                                       || EF.Functions.Like(r.Product.Description, pattern, LikeEscape)
                                       || EF.Functions.Like(r.CategoryName, pattern, LikeEscape));
        }

        if (query.MinPrice is { } min)
        {
            source = source.Where(r => r.Effective >= min);
        }

        if (query.MaxPrice is { } max)
        {
            source = source.Where(r => r.Effective <= max);
        }

        var ordered = query.Order == ProductOrder.Price
            ? source
                .OrderBy(r => r.Effective)
                .ThenBy(r => r.Product.Id)
            : source
                .OrderByDescending(r => r.Product.CreatedAt)
                .ThenByDescending(r => r.Product.Id);

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
                Total = source.Count()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new ProductRow(r.Product, r.Slug, r.SoldOut)).ToList();
        return (items, rows.Count == 0 ? 0 : rows[0].Total);
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
