using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfOrderDal : EfEntityRepositoryBase<Order, HerYerdeContext>, IOrderDal
{
    public EfOrderDal(HerYerdeContext context) : base(context)
    {
    }

    public async Task<long> NextOrderSequenceAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Context.Database
            .SqlQueryRaw<long>("SELECT NEXT VALUE FOR order_no_seq AS Value")
            .ToListAsync(cancellationToken);

        return rows[0];
    }

    public Task<int> TryChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to, CancellationToken cancellationToken = default)
        => Context.Orders
            .Where(o => o.Id == orderId && o.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, to), cancellationToken);

    public Task<int> UnseenCountAsync(CancellationToken cancellationToken = default)
        => Context.Orders.CountAsync(o => o.SeenAt == null, cancellationToken);
}
