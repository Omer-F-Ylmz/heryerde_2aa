using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfContactMessageDal : EfEntityRepositoryBase<ContactMessage, HerYerdeContext>, IContactMessageDal
{
    public EfContactMessageDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<List<ContactMessage>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
        => Context.ContactMessages.AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}
