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

    /// <summary>Kuyruğa giriş anı; hazırlık denetimi bekleyen en eski kaydın gecikmesini buradan ölçer.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Başarısız denemeden sonraki en erken yeniden deneme anı; ilk kayıtta boştur.</summary>
    public DateTime? NextTryAt { get; set; }

    public DateTime? SentAt { get; set; }

    /// <summary>Postaya eklenecek belgenin gizli depodaki göreli yolu (ör. sipariş anında onaylanan sözleşme sürümü); yoksa boş.</summary>
    public string? Attachment { get; set; }
}

public static class OutboxType
{
    public const string OrderPlaced = "siparis-alindi";
    public const string OrderShipped = "siparis-kargoda";
    public const string NewOrderForStore = "yeni-siparis";
    public const string ContactMessage = "iletisim-mesaji";
    public const string AdminPasswordReset = "yonetici-sifre-sifirlama";
    public const string PaymentApproved = "havale-onaylandi";
    public const string ReturnApproved = "iade-onaylandi";
    public const string ReturnRejected = "iade-reddedildi";
    public const string InvoiceReady = "fatura-hazir";
    public const string ReviewInvite = "degerlendirme-daveti";
    public const string GiftRegistryCreated = "ceyiz-listesi-yonetim";
    public const string GiftRegistryPurchase = "ceyiz-listesi-hediye";
    public const string CustomerVerify = "hesap-dogrulama";
    public const string CustomerLoginLink = "hesap-giris-baglantisi";
    public const string CustomerPasswordReset = "hesap-sifre-sifirlama";
}
