using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfAnnouncementDal : EfEntityRepositoryBase<Announcement, HerYerdeContext>, IAnnouncementDal
{
    public EfAnnouncementDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<Announcement?> CurrentAsync(DateTime moment, CancellationToken cancellationToken = default)
        => Context.Announcements.AsNoTracking()
            .Where(a => a.IsActive && a.StartsAt <= moment && a.EndsAt > moment)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<Announcement>> GetLatestAsync(int take, CancellationToken cancellationToken = default)
        => Context.Announcements.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}
