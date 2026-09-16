using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

/// <summary>Rapor satırı: gün, olay, yol, kaynak ve cihaz başına görüntülenme ve yaklaşık ziyaret; ham kayıttan ya da günlük özetten.</summary>
public sealed class AnalyticsRow
{
    public DateTime Day { get; init; }
    public string Event { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Device { get; init; } = string.Empty;
    public int Views { get; init; }
    public int Visits { get; init; }
}

public interface IPageViewDal : IEntityRepository<PageView>
{
    Task AddRangeAsync(IEnumerable<PageView> views, CancellationToken cancellationToken = default);

    /// <summary>[from, to] günleri: ham kayıtlar gruplanmış (ziyaret = aynı yol + yönlendiren için ayrı yarım saat dilimi sayısı)
    /// ve günlük özet satırları; ikisi aynı günü içermez (özetleme ham kaydı aynı işlemde siler).</summary>
    Task<List<AnalyticsRow>> GetRowsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary><paramref name="cutoff"/>'tan önceki ham kayıtların günlük özet satırları (kaydedilmemiş, izlenmeyen nesneler).</summary>
    Task<List<AnalyticsDaily>> SummarizeBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default);

    /// <summary>Verilen günlerin özet satırları, güncellenmek üzere izlenir.</summary>
    Task<List<AnalyticsDaily>> GetTrackedDailiesAsync(IReadOnlyCollection<DateTime> days, CancellationToken cancellationToken = default);

    void AddDaily(AnalyticsDaily daily);

    Task<int> DeleteBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
