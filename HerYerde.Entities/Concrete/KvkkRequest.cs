using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>İlgili kişinin KVKK m. 11 başvurusu; en geç 30 gün içinde yanıtlanır (m. 13). Kişi tek biçime indirgenmiş telefon ya da
/// küçük harf e-postadır.</summary>
public class KvkkRequest : IEntity
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;

    /// <summary>Başvurunun veri sorumlusuna ulaştığı an; süre buradan sayılır.</summary>
    public DateTime ReceivedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
