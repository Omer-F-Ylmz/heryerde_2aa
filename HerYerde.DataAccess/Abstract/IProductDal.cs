using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IProductDal : IEntityRepository<Product>
{
    /// <summary>Yayındaki ürünler, kendi kategorilerinin slug'ıyla birlikte tek sorguda.</summary>
    Task<List<(Product Product, string CategorySlug)>> GetActiveWithCategorySlugAsync(CancellationToken cancellationToken = default);
}
