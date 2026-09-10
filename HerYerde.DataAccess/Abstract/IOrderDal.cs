using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IOrderDal : IEntityRepository<Order>
{
    /// <summary>Sipariş numarasının sıra kısmını veritabanı SEQUENCE'ından alır; eşzamanlı isteklerde
    /// her çağrı farklı değer döner, sıra gün başında sıfırlanmaz.</summary>
    Task<long> NextOrderSequenceAsync(CancellationToken cancellationToken = default);
}
