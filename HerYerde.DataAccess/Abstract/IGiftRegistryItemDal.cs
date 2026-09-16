using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IGiftRegistryItemDal : IEntityRepository<GiftRegistryItem>
{
    /// <summary>Alınan adedi tek UPDATE ile artırır; eşzamanlı iki sipariş birbirinin artışını ezmez.</summary>
    Task<int> IncrementReceivedAsync(int itemId, int quantity, CancellationToken cancellationToken = default);
}
