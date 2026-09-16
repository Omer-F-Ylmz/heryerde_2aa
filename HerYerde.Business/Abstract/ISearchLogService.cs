using HerYerde.Business.Dtos;

namespace HerYerde.Business.Abstract;

/// <summary>Arama günlüğü (D15): vitrin aramalarının terimi ve sonuç sayısı; yönetim raporu ve saklama süresi temizliği.</summary>
public interface ISearchLogService
{
    Task RecordAsync(string term, int resultCount, CancellationToken cancellationToken = default);

    /// <summary>Son <paramref name="window"/> içinde sonuçsuz ve en çok aranan terimler.</summary>
    Task<SearchReport> GetReportAsync(TimeSpan window, CancellationToken cancellationToken = default);

    Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default);
}
