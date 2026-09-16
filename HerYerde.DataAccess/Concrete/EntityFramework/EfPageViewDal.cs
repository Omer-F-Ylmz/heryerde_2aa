using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfPageViewDal : EfEntityRepositoryBase<PageView, HerYerdeContext>, IPageViewDal
{
    public EfPageViewDal(HerYerdeContext context) : base(context)
    {
    }

    public async Task AddRangeAsync(IEnumerable<PageView> views, CancellationToken cancellationToken = default)
        => await Context.PageViews.AddRangeAsync(views, cancellationToken);

    public async Task<List<AnalyticsRow>> GetRowsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var raw = await Grouped(Context.PageViews.AsNoTracking().Where(p => p.Day >= from && p.Day <= to))
            .ToListAsync(cancellationToken);

        var daily = await Context.AnalyticsDailies.AsNoTracking()
            .Where(d => d.Day >= from && d.Day <= to)
            .Select(d => new AnalyticsRow { Day = d.Day, Event = d.Event, Path = d.Path, Source = d.Source, Device = d.Device, Views = d.Views, Visits = d.Visits })
            .ToListAsync(cancellationToken);

        return raw.Concat(daily).ToList();
    }

    public async Task<List<AnalyticsDaily>> SummarizeBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        => (await Grouped(Context.PageViews.AsNoTracking().Where(p => p.Day < cutoff)).ToListAsync(cancellationToken))
            .Select(r => new AnalyticsDaily { Day = r.Day, Event = r.Event, Path = r.Path, Source = r.Source, Device = r.Device, Views = r.Views, Visits = r.Visits })
            .ToList();

    public Task<List<AnalyticsDaily>> GetTrackedDailiesAsync(IReadOnlyCollection<DateTime> days, CancellationToken cancellationToken = default)
        => Context.AnalyticsDailies.Where(d => days.Contains(d.Day)).ToListAsync(cancellationToken);

    public void AddDaily(AnalyticsDaily daily) => Context.AnalyticsDailies.Add(daily);

    public Task<int> DeleteBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        => Context.PageViews.Where(p => p.Day < cutoff).ExecuteDeleteAsync(cancellationToken);

    /// <summary>Kaynak: UTM kaynağı, yoksa yönlendiren alan adı, o da yoksa boş (doğrudan ya da site içi). Ziyaret: grupta ayrı
    /// (yarım saat dilimi, yönlendiren) çiftleri.</summary>
    private static IQueryable<AnalyticsRow> Grouped(IQueryable<PageView> views)
        => views
            .GroupBy(p => new { p.Day, p.Event, p.Path, Source = p.UtmSource ?? p.ReferrerHost ?? "", p.Device })
            .Select(g => new AnalyticsRow
            {
                Day = g.Key.Day,
                Event = g.Key.Event,
                Path = g.Key.Path,
                Source = g.Key.Source,
                Device = g.Key.Device,
                Views = g.Count(),
                Visits = g.Select(p => (p.Hour * 2 + (p.HalfHour ? 1 : 0)).ToString() + "|" + (p.ReferrerHost ?? "")).Distinct().Count()
            });
}
