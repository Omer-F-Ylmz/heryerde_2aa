using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Saklama süresi dolan analitik kayıtlarının günlük özeti (D16): gün, olay, yol, kaynak (UTM kaynağı ya da yönlendiren
/// alan adı; boş = doğrudan/site içi) ve cihaz başına görüntülenme ve yaklaşık ziyaret sayısı.</summary>
public class AnalyticsDaily : IEntity
{
    public int Id { get; set; }
    public DateTime Day { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public int Views { get; set; }
    public int Visits { get; set; }
}
