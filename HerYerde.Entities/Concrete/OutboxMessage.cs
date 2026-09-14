using HerYerde.Core.Entities;
using HerYerde.Entities.Enums;

namespace HerYerde.Entities.Concrete;

/// <summary>Gönderilecek e-posta; sipariş işlemiyle aynı veritabanı işleminde yazılır, gönderim arka planda
/// olur. Böylece SMTP çökse de sipariş kaybolmaz.</summary>
public class OutboxMessage : IEntity
{
    public int Id { get; set; }

    /// <summary>Bkz. <see cref="OutboxType"/>.</summary>
    public string Type { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; }
    public int TryCount { get; set; }

    /// <summary>Başarısız denemeden sonraki en erken yeniden deneme anı; ilk kayıtta boştur.</summary>
    public DateTime? NextTryAt { get; set; }

    public DateTime? SentAt { get; set; }
}

public static class OutboxType
{
    public const string OrderPlaced = "siparis-alindi";
    public const string OrderShipped = "siparis-kargoda";
    public const string NewOrderForStore = "yeni-siparis";
}
