using HerYerde.Business.Dtos;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

/// <summary>Çerezsiz analitik (D16): toplu kayıt, rapor ve saklama süresi dolan ham kaydın günlük özete toplanması.</summary>
public interface IAnalyticsService
{
    Task RecordAsync(IReadOnlyCollection<PageView> batch, CancellationToken cancellationToken = default);

    Task<AnalyticsReport> GetReportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary><paramref name="today"/>'den 90 günden eski ham kayıtları günlük özete ekler ve siler (tek işlem); silinen sayısı.</summary>
    Task<int> RollupAsync(DateOnly today, CancellationToken cancellationToken = default);
}
