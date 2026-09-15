using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Enums;
using Microsoft.Extensions.Options;

namespace HerYerde.Business.Concrete;

public class DashboardManager : IDashboardService
{
    private readonly IOrderDal _orderDal;
    private readonly IProductDal _productDal;
    private readonly ShopSettings _shop;
    private readonly TimeProvider _clock;

    public DashboardManager(IOrderDal orderDal, IProductDal productDal, IOptions<ShopSettings> shop, TimeProvider clock)
    {
        _orderDal = orderDal;
        _productDal = productDal;
        _shop = shop.Value;
        _clock = clock;
    }

    public async Task<DashboardView> GetAsync(TimeZoneInfo zone, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var today = LocalDay(now, zone);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var counts = await _orderDal.DashboardCountsAsync(StartUtc(today, zone), StartUtc(monday, zone), cancellationToken);
        var lowStock = await _productDal.CountLowStockAsync(_shop.LowStockAlertAt, cancellationToken);

        // Onaylanıp hâlâ hazırlanmamış sipariş, verilen iş günü sözünü geçtiyse gecikmiştir.
        var late = (await _orderDal.GetListAsync(o => o.Status == OrderStatus.Onaylandi, cancellationToken))
            .Where(o => BusinessDaysBetween(LocalDay(o.CreatedAt, zone), today) > _shop.DispatchDays)
            .OrderBy(o => o.CreatedAt)
            .ToList();

        return new DashboardView(
            counts.TodayOrders,
            counts.TodayRevenue,
            counts.WeekOrders,
            counts.WeekRevenue,
            counts.PendingNotices,
            counts.PendingReturns,
            counts.PendingReviews,
            lowStock,
            counts.UnreadMessages,
            late);
    }

    /// <summary>(from, to] aralığındaki hafta içi günler: cuma verilen sipariş için pazartesi 1. iş günüdür.</summary>
    public static int BusinessDaysBetween(DateOnly from, DateOnly to)
    {
        var days = 0;
        for (var day = from.AddDays(1); day <= to; day = day.AddDays(1))
        {
            days += day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0 : 1;
        }

        return days;
    }

    private static DateOnly LocalDay(DateTime utc, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));

    private static DateTime StartUtc(DateOnly day, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
}
