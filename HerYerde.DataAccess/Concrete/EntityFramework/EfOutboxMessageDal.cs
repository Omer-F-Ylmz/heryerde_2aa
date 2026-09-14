using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfOutboxMessageDal : EfEntityRepositoryBase<OutboxMessage, HerYerdeContext>, IOutboxMessageDal
{
    public EfOutboxMessageDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<List<OutboxMessage>> DueAsync(DateTime moment, int take, CancellationToken cancellationToken = default)
        => Context.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Bekliyor && (m.NextTryAt == null || m.NextTryAt <= moment))
            .OrderBy(m => m.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}
