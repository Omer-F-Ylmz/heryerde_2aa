using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfAdminAuditLogDal : EfEntityRepositoryBase<AdminAuditLog, HerYerdeContext>, IAdminAuditLogDal
{
    public EfAdminAuditLogDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<List<AdminAuditLog>> GetRecentAsync(int skip, int take, CancellationToken cancellationToken = default)
        => Context.AdminAuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Context.AdminAuditLogs.CountAsync(cancellationToken);
}
