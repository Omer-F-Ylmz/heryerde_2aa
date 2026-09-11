using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IAdminAuditLogDal : IEntityRepository<AdminAuditLog>
{
    /// <summary>En yeniden eskiye; sayfalama SQL'de.</summary>
    Task<List<AdminAuditLog>> GetRecentAsync(int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
