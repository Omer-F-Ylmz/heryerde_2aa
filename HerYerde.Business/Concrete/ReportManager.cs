using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Concrete;

public class ReportManager : IReportService
{
    private const int TopCount = 10;

    private readonly IOrderDal _orderDal;
    private readonly IOrderItemDal _orderItemDal;
    private readonly IPaymentDal _paymentDal;

    public ReportManager(IOrderDal orderDal, IOrderItemDal orderItemDal, IPaymentDal paymentDal)
    {
        _orderDal = orderDal;
        _orderItemDal = orderItemDal;
        _paymentDal = paymentDal;
    }

    public async Task<SalesReport> BuildAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ReportPeriod period,
        TimeZoneInfo zone,
        CancellationToken cancellationToken = default)
    {
        // Küçük mağaza hacmi: aralığın siparişleri, kalemleri ve kart ödemeleri üç sorguda belleğe alınır.
        var orders = await _orderDal.GetListAsync(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc, cancellationToken);
        var ids = orders.Select(o => o.Id).ToList();
        var items = ids.Count == 0 ? new List<OrderItem>() : await _orderItemDal.GetListAsync(i => ids.Contains(i.OrderId) && !i.IsGift, cancellationToken);
        var paidCards = ids.Count == 0
            ? new HashSet<int>()
            : (await _paymentDal.GetListAsync(p => ids.Contains(p.OrderId) && p.Status == PaymentStatus.Basarili, cancellationToken))
                .Select(p => p.OrderId)
                .ToHashSet();

        var open = orders.Where(o => o.Status != OrderStatus.IptalEdildi).ToList();
        var paid = open.Where(o => IsPaid(o, paidCards)).ToList();
        var revenue = paid.Sum(o => o.Total);
        var openIds = open.Select(o => o.Id).ToHashSet();
        var paidIds = paid.Select(o => o.Id).ToHashSet();

        return new SalesReport(
            revenue,
            orders.Count,
            paid.Count,
            paid.Count == 0 ? 0m : Math.Round(revenue / paid.Count, 2),
            orders.Count == 0 ? 0m : Math.Round((decimal)(orders.Count - open.Count) / orders.Count, 4),
            orders
                .GroupBy(o => PeriodStart(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc), zone)), period))
                .OrderBy(g => g.Key)
                .Select(g => new ReportPeriodRow(g.Key, g.Where(o => paidIds.Contains(o.Id)).Sum(o => o.Total), g.Count()))
                .ToList(),
            items
                .Where(i => openIds.Contains(i.OrderId))
                .GroupBy(i => (i.ProductName, i.Sku))
                .Select(g => new TopProductRow(g.Key.ProductName, g.Key.Sku, g.Sum(i => i.Quantity), g.Sum(i => i.UnitPrice * i.Quantity)))
                .OrderByDescending(p => p.Quantity)
                .ThenByDescending(p => p.Amount)
                .ThenBy(p => p.ProductName, StringComparer.Ordinal)
                .ThenBy(p => p.Sku, StringComparer.Ordinal)
                .Take(TopCount)
                .ToList(),
            open
                .GroupBy(o => o.Source)
                .Select(g => new SourceRow(g.Key, g.Count(), g.Where(o => paidIds.Contains(o.Id)).Sum(o => o.Total)))
                .OrderByDescending(s => s.Orders)
                .ToList(),
            open
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new PaymentMethodRow(g.Key, g.Count(), g.Where(o => paidIds.Contains(o.Id)).Sum(o => o.Total)))
                .OrderByDescending(m => m.Orders)
                .ToList());
    }

    /// <summary>Para kasaya girdi mi: kart çekimi başarılı, havale onaylandı (sonraki durumlar dahil), kapıda ya da elden
    /// ödemede teslim edildi. Beklemedeki havale ve yoldaki kapıda ödeme ciroya girmez.</summary>
    private static bool IsPaid(Order order, HashSet<int> paidCards) => order.PaymentMethod switch
    {
        PaymentMethod.KrediKarti => paidCards.Contains(order.Id),
        PaymentMethod.HavaleEft => order.Status != OrderStatus.Beklemede,
        _ => order.Status == OrderStatus.TeslimEdildi
    };

    /// <summary>Hafta pazartesi başlar.</summary>
    private static DateOnly PeriodStart(DateOnly day, ReportPeriod period) => period switch
    {
        ReportPeriod.Hafta => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        ReportPeriod.Ay => new DateOnly(day.Year, day.Month, 1),
        _ => day
    };
}
