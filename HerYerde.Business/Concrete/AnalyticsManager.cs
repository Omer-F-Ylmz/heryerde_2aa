using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class AnalyticsManager : IAnalyticsService
{
    /// <summary>Ham kayıt bu kadar gün tutulur; daha eskisi yalnız günlük özette kalır.</summary>
    public const int RetentionDays = 90;

    private const int ListSize = 10;

    private readonly IPageViewDal _pageViewDal;
    private readonly IUnitOfWork _unitOfWork;

    public AnalyticsManager(IPageViewDal pageViewDal, IUnitOfWork unitOfWork)
    {
        _pageViewDal = pageViewDal;
        _unitOfWork = unitOfWork;
    }

    public async Task RecordAsync(IReadOnlyCollection<PageView> batch, CancellationToken cancellationToken = default)
    {
        await _pageViewDal.AddRangeAsync(batch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnalyticsReport> GetReportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rows = await _pageViewDal.GetRowsAsync(from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue), cancellationToken);
        var views = rows.Where(r => r.Event == AnalyticsEvent.View).ToList();
        int Count(string name) => rows.Where(r => r.Event == name).Sum(r => r.Views);

        return new AnalyticsReport(
            from,
            to,
            views.Sum(r => r.Views),
            views.Sum(r => r.Visits),
            views.GroupBy(r => r.Day)
                .OrderBy(g => g.Key)
                .Select(g => new AnalyticsDayRow(DateOnly.FromDateTime(g.Key), g.Sum(r => r.Views), g.Sum(r => r.Visits)))
                .ToList(),
            Ranked(views.Where(r => AnalyticsRules.IsProductPath(r.Path)), r => r.Path),
            Ranked(views.Where(r => AnalyticsRules.IsCategoryPath(r.Path)), r => r.Path),
            Ranked(views, r => r.Source),
            Ranked(views, r => r.Device),
            new AnalyticsFunnel(
                views.Where(r => AnalyticsRules.IsProductPath(r.Path)).Sum(r => r.Views),
                Count(AnalyticsEvent.AddToCart),
                Count(AnalyticsEvent.Checkout),
                Count(AnalyticsEvent.Order)),
            Count(AnalyticsEvent.WhatsApp),
            Count(AnalyticsEvent.Instagram),
            Count(AnalyticsEvent.Search));
    }

    public Task<int> RollupAsync(DateOnly today, CancellationToken cancellationToken = default)
        => _unitOfWork.InTransactionAsync(async () =>
        {
            var cutoff = today.AddDays(-RetentionDays).ToDateTime(TimeOnly.MinValue);
            var summaries = await _pageViewDal.SummarizeBeforeAsync(cutoff, cancellationToken);
            if (summaries.Count == 0)
            {
                return 0;
            }

            // Aynı gün önceden özetlenmişse (geç gelen ham kayıt) satır yeniden yazılmaz, sayılar eklenir.
            var existing = await _pageViewDal.GetTrackedDailiesAsync(summaries.Select(s => s.Day).Distinct().ToList(), cancellationToken);
            foreach (var summary in summaries)
            {
                if (existing.FirstOrDefault(d => d.Day == summary.Day && d.Event == summary.Event && d.Path == summary.Path
                                                 && d.Source == summary.Source && d.Device == summary.Device) is { } stored)
                {
                    stored.Views += summary.Views;
                    stored.Visits += summary.Visits;
                }
                else
                {
                    _pageViewDal.AddDaily(summary);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await _pageViewDal.DeleteBeforeAsync(cutoff, cancellationToken);
        }, cancellationToken);

    private static List<AnalyticsCount> Ranked(IEnumerable<AnalyticsRow> rows, Func<AnalyticsRow, string> label)
        => rows.GroupBy(label)
            .Select(g => new AnalyticsCount(g.Key, g.Sum(r => r.Views)))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Label, StringComparer.Ordinal)
            .Take(ListSize)
            .ToList();
}
