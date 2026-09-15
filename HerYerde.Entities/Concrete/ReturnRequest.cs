using HerYerde.Core.Entities;
using HerYerde.Entities.Enums;

namespace HerYerde.Entities.Concrete;

/// <summary>Müşterinin teslimden sonra sipariş sayfasından açtığı cayma/iade ya da değişim talebi; kalemleri
/// <see cref="ReturnRequestItem"/>'da. Yönetici onaylar ya da gerekçeyle reddeder, ürün gelince teslim alır.</summary>
public class ReturnRequest : IEntity
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public ReturnType Type { get; set; }
    public ReturnStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>Müşterinin eklediği fotoğrafın gizli depodaki göreli yolu; eklenmediyse boş.</summary>
    public string? PhotoFile { get; set; }

    /// <summary>Kartla ödenmemiş siparişte iadenin yatırılacağı IBAN (boşluksuz); kartta boş.</summary>
    public string? RefundIban { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Onay ya da ret anı.</summary>
    public DateTime? DecidedAt { get; set; }

    public string? RejectReason { get; set; }

    /// <summary>Ürünün mağazaya geri geldiği an; stok bu anda iade edilir.</summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Geri ödenen tutar (kalemlerin satır tutarı; tüm kalemler dönerse kargo dahil); değişimde 0.</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>Kartta sağlayıcı iadesinin, havale/kapıda ödemede yöneticinin "iade edildi" işaretinin anı.</summary>
    public DateTime? RefundedAt { get; set; }
}

/// <summary>Talebe konu sipariş kalemi ve adedi; değişimde istenen varyantın stok kodu.</summary>
public class ReturnRequestItem : IEntity
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public int OrderItemId { get; set; }
    public int Quantity { get; set; }
    public string? NewSku { get; set; }
}
