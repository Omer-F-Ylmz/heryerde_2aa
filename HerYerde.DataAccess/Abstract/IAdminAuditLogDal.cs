using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IAdminAuditLogDal : IEntityRepository<AdminAuditLog>
{
    /// <summary>En yeniden eskiye, yönetici e-postasıyla; sayfalama SQL'de.</summary>
    Task<List<AdminAuditRow>> GetRecentAsync(int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
