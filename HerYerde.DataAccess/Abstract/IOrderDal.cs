using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IOrderDal : IEntityRepository<Order>
{
    /// <summary>Gün içindeki son sipariş numarasını verir; yoksa null. Sipariş numarası sırası buradan devam eder.</summary>
    Task<string?> LastOrderNoOfDayAsync(string prefix, CancellationToken cancellationToken = default);
}
