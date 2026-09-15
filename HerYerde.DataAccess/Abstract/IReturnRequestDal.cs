using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.DataAccess.Abstract;

public interface IReturnRequestDal : IEntityRepository<ReturnRequest>
{
    /// <summary>Talebi yalnız hâlâ <paramref name="from"/> durumundaysa <paramref name="to"/> yapar; tek koşullu UPDATE, eşzamanlı
    /// ikinci işlem satır kilidini bekleyip 0 alır.</summary>
    Task<int> TryMoveAsync(int id, ReturnStatus from, ReturnStatus to, CancellationToken cancellationToken = default);
}

public interface IReturnRequestItemDal : IEntityRepository<ReturnRequestItem>
{
}
