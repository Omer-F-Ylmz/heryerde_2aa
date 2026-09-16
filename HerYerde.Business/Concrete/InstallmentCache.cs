using System.Collections.Concurrent;
using HerYerde.Business.Dtos;

namespace HerYerde.Business.Concrete;

/// <summary>Taksit tablolarının süreli önbelleği; tekil, tüm istekler paylaşır. Tablo tutara ve BIN'e bağlı,
/// kişisel veri taşımaz.</summary>
public sealed class InstallmentCache
{
    /// <summary>Bu sayıya ulaşınca yazmadan önce süresi geçenler atılır; BIN sayısı sınırsız olduğu için gerekli.</summary>
    private const int PruneAt = 256;

    private readonly ConcurrentDictionary<string, (DateTime ExpiresAt, InstallmentTable Table)> _entries = new(StringComparer.Ordinal);

    public InstallmentTable? Get(string key, DateTime now)
        => _entries.TryGetValue(key, out var entry) && entry.ExpiresAt > now ? entry.Table : null;

    public void Set(string key, InstallmentTable table, DateTime now, TimeSpan lifetime)
    {
        if (_entries.Count >= PruneAt)
        {
            foreach (var (stale, entry) in _entries.Where(e => e.Value.ExpiresAt <= now))
            {
                _entries.TryRemove(stale, out _);
            }
        }

        _entries[key] = (now + lifetime, table);
    }
}
