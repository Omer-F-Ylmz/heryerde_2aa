using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfOrderDal : EfEntityRepositoryBase<Order, HerYerdeContext>, IOrderDal
{
    public EfOrderDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<string?> LastOrderNoOfDayAsync(string prefix, CancellationToken cancellationToken = default)
        => Context.Orders
            .AsNoTracking()
            .Where(o => o.OrderNo.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNo)
            .Select(o => o.OrderNo)
            .FirstOrDefaultAsync(cancellationToken);
}
