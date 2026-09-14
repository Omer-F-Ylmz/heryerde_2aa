using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IOutboxMessageDal : IEntityRepository<OutboxMessage>
{
    /// <summary>Gönderilmeyi bekleyen, yeniden deneme anı gelmiş kayıtlar; izlenir, dağıtımda güncellenir.</summary>
    Task<List<OutboxMessage>> DueAsync(DateTime moment, int take, CancellationToken cancellationToken = default);
}
