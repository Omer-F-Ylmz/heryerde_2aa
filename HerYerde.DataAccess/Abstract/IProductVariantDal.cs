using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IProductVariantDal : IEntityRepository<ProductVariant>
{
    /// <summary>Stok yeterliyse tek UPDATE ile düşer; etkilenen satır sayısını döner (0 = stok yetmedi).</summary>
    Task<int> TryDecrementStockAsync(int variantId, int quantity, CancellationToken cancellationToken = default);
}
