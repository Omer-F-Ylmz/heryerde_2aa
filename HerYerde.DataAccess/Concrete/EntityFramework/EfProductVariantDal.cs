using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfProductVariantDal : EfEntityRepositoryBase<ProductVariant, HerYerdeContext>, IProductVariantDal
{
    public EfProductVariantDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<int> TryDecrementStockAsync(int variantId, int quantity, CancellationToken cancellationToken = default)
        => Context.ProductVariants
            .Where(v => v.Id == variantId && v.Stock >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock - quantity), cancellationToken);

    public Task<int> IncrementStockBySkuAsync(string sku, int quantity, CancellationToken cancellationToken = default)
        => Context.ProductVariants
            .Where(v => v.Sku == sku)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock + quantity), cancellationToken);
}
