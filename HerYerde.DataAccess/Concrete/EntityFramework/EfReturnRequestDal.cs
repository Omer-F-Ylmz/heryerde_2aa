using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfReturnRequestDal : EfEntityRepositoryBase<ReturnRequest, HerYerdeContext>, IReturnRequestDal
{
    public EfReturnRequestDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<int> TryMoveAsync(int id, ReturnStatus from, ReturnStatus to, CancellationToken cancellationToken = default)
        => Context.ReturnRequests
            .Where(r => r.Id == id && r.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, to), cancellationToken);
}

public class EfReturnRequestItemDal : EfEntityRepositoryBase<ReturnRequestItem, HerYerdeContext>, IReturnRequestItemDal
{
    public EfReturnRequestItemDal(HerYerdeContext context) : base(context)
    {
    }
}
