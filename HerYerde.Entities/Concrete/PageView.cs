using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Çerezsiz analitik kaydı (D16): sayfa görüntüleme ya da olay (sepete ekleme, ödeme, sipariş, arama, tıklama).
/// IP, tarayıcı kimliği, çerez ve oturum tutulmaz; yalnız yol, yönlendiren alan adı, UTM, cihaz sınıfı ve İstanbul saatiyle
/// gün/saat/yarım saat. 90 gün sonra günlük özete toplanıp silinir.</summary>
public class PageView : IEntity
{
    public long Id { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? ReferrerHost { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }

    /// <summary>"mobil", "tablet" ya da "masaustu".</summary>
    public string Device { get; set; } = string.Empty;

    public DateTime Day { get; set; }
    public byte Hour { get; set; }

    /// <summary>Saatin ikinci yarısı (dakika ≥ 30); ziyaret yaklaşımı yarım saatlik dilime bakar.</summary>
    public bool HalfHour { get; set; }
}
