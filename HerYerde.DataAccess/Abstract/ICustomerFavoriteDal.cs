using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface ICustomerFavoriteDal : IEntityRepository<CustomerFavorite>
{
    /// <summary>Üyenin favori ürün kimlikleri; vitrin kartlarındaki kalbi tek sorguda işaretler.</summary>
    Task<HashSet<int>> ProductIdsAsync(int customerId, CancellationToken cancellationToken = default);
}
