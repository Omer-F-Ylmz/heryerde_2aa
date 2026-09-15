using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IKvkkRequestDal : IEntityRepository<KvkkRequest>
{
}

public interface IOrderNoteDal : IEntityRepository<OrderNote>
{
}

/// <summary>Pano sayaçları; tek SQL'in sonucu (kolon adları özellik adlarıyla aynı).</summary>
public sealed class DashboardCounts
{
    public int TodayOrders { get; init; }
    public decimal TodayRevenue { get; init; }
    public int WeekOrders { get; init; }
    public decimal WeekRevenue { get; init; }
    public int PendingNotices { get; init; }
    public int PendingReturns { get; init; }
    public int PendingReviews { get; init; }
    public int UnreadMessages { get; init; }
}
