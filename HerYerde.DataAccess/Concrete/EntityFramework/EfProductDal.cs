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

    public async Task<(List<(Product Product, string CategorySlug)> Items, int Total)> GetActiveAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = Context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Join(Context.Categories, p => p.CategoryId, c => c.Id, (p, c) => new { Product = p, c.Slug });

        if (query.CategoryIds.Count > 0)
        {
            source = source.Where(r => query.CategoryIds.Contains(r.Product.CategoryId));
        }

        if (query.ExcludedProductId is { } excluded)
        {
            source = source.Where(r => r.Product.Id != excluded);
        }

        var ordered = query.Order == ProductOrder.Price
            ? source
                .OrderBy(r => r.Product.CampaignPrice != null
                              && r.Product.CampaignPrice < r.Product.Price
                              && (r.Product.CampaignEndsAt == null || r.Product.CampaignEndsAt > query.Now)
                    ? r.Product.CampaignPrice!.Value
                    : r.Product.Price)
                .ThenBy(r => r.Product.Id)
            : source
                .OrderByDescending(r => r.Product.CreatedAt)
                .ThenByDescending(r => r.Product.Id);

        // Toplam sayı, sayfa satırlarının yanında alt sorgu olarak gelir: sayfalama tek gidiş-dönüş.
        var rows = await ordered
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(r => new { r.Product, r.Slug, Total = source.Count() })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => (r.Product, r.Slug)).ToList();
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
}
