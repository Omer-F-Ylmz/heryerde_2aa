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

    /// <summary>Ciro kuralı ReportManager.IsPaid ile aynı: iptal dışı; kartta başarılı çekim, havalede onay (Beklemede değil),
    /// kapıda/elden ödemede teslim. Durum/yöntem değerleri enum kimlikleridir (OrderStatus, PaymentMethod, PaymentStatus).</summary>
    public Task<DashboardCounts> DashboardCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken = default)
        => Context.Database
            .SqlQuery<DashboardCounts>($"""
                SELECT
                    (SELECT COUNT(*) FROM [order] WHERE created_at >= {todayStartUtc} AND status <> 5) AS TodayOrders,
                    (SELECT COALESCE(SUM(o.total), 0) FROM [order] o WHERE o.created_at >= {todayStartUtc} AND o.status <> 5 AND (
                        (o.payment_method = 3 AND EXISTS (SELECT 1 FROM payment p WHERE p.order_id = o.id AND p.status = 2))
                        OR (o.payment_method = 2 AND o.status <> 1)
                        OR (o.payment_method IN (1, 4) AND o.status = 4))) AS TodayRevenue,
                    (SELECT COUNT(*) FROM [order] WHERE created_at >= {weekStartUtc} AND status <> 5) AS WeekOrders,
                    (SELECT COALESCE(SUM(o.total), 0) FROM [order] o WHERE o.created_at >= {weekStartUtc} AND o.status <> 5 AND (
                        (o.payment_method = 3 AND EXISTS (SELECT 1 FROM payment p WHERE p.order_id = o.id AND p.status = 2))
                        OR (o.payment_method = 2 AND o.status <> 1)
                        OR (o.payment_method IN (1, 4) AND o.status = 4))) AS WeekRevenue,
                    (SELECT COUNT(*) FROM payment_notice n JOIN [order] o ON o.id = n.order_id WHERE n.approved_at IS NULL AND o.status = 1) AS PendingNotices,
                    (SELECT COUNT(*) FROM return_request WHERE status = 1) AS PendingReturns,
                    (SELECT COUNT(*) FROM product_review WHERE is_approved = 0) AS PendingReviews,
                    (SELECT COUNT(*) FROM contact_message WHERE read_at IS NULL) AS UnreadMessages
                """)
            .SingleAsync(cancellationToken);

    public Task<int> UnseenCountAsync(CancellationToken cancellationToken = default)
        => Context.Orders.CountAsync(o => o.SeenAt == null, cancellationToken);
}
