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

    public Task<List<AdminAuditRow>> GetRecentAsync(int skip, int take, CancellationToken cancellationToken = default)
        => (from entry in Context.AdminAuditLogs.AsNoTracking()
            join admin in Context.AdminUsers on entry.AdminId equals admin.Id into admins
            from admin in admins.DefaultIfEmpty()
            orderby entry.At descending, entry.Id descending
            select new AdminAuditRow(entry, admin == null ? null : admin.Email))
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Context.AdminAuditLogs.CountAsync(cancellationToken);
}
