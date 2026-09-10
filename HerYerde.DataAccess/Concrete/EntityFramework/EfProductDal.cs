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

    public async Task<List<(Product Product, string CategorySlug)>> GetActiveWithCategorySlugAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Join(Context.Categories, p => p.CategoryId, c => c.Id, (p, c) => new { Product = p, c.Slug })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Product, r.Slug)).ToList();
    }

    public Task<bool> SlugTakenAsync(string slug, int excludedId, CancellationToken cancellationToken = default)
        => Context.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(p => p.Slug == slug && p.Id != excludedId, cancellationToken);
}
