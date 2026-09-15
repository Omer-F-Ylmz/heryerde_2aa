using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Müşterinin "havaleyi yaptım" bildirimi; yönetici hesabı kontrol edip onaylar. Dekont isteğe bağlı.</summary>
public class PaymentNotice : IEntity
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    /// <summary>Havaleyi gönderen hesabın sahibi; siparişteki addan farklı olabilir.</summary>
    public string SenderName { get; set; } = string.Empty;

    public DateTime PaidOn { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Dekontun gizli depodaki göreli yolu; yüklenmediyse boş.</summary>
    public string? ReceiptFile { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Yönetici onayladığı an; boşsa bildirim bekliyor.</summary>
    public DateTime? ApprovedAt { get; set; }
}
