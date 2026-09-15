using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Dtos;

/// <summary>Ödeme formundan gelen müşteri bilgisi; telefon doğrulama sırasında tek biçime indirgenir.</summary>
public sealed record OrderDraft(
    string FullName,
    string Phone,
    string? Email,
    string Address,
    string City,
    string District,
    string? Note,
    PaymentMethod PaymentMethod);

/// <summary>Payment yalnız kartla ödenen siparişte dolu; Notices yalnız havale siparişinde.</summary>
public sealed record OrderDetail(Order Order, IReadOnlyList<OrderItem> Items, Payment? Payment = null, IReadOnlyList<PaymentNotice>? Notices = null);

/// <summary>Yönetimden girilen siparişin satırı: Code varyantın stok kodu ya da varyantsız ürünün slug'ı.</summary>
public sealed record ManualOrderLine(string Code, int Quantity);

/// <summary>WhatsApp/Instagram/telefon/mağaza siparişi. ShippingFeeOverride boşsa kargo kuraldan gelir;
/// NotifyCustomer ise e-postası olan müşteriye "sipariş alındı" postası gider.</summary>
public sealed record ManualOrderDraft(
    string FullName,
    string Phone,
    string? Email,
    string Address,
    string City,
    string District,
    string? Note,
    PaymentMethod PaymentMethod,
    OrderSource Source,
    IReadOnlyList<ManualOrderLine> Lines,
    decimal? ShippingFeeOverride,
    bool NotifyCustomer,
    bool ConsentConfirmed = false);

/// <summary>Sipariş düzenleme: teslimat bilgisi ve kalem adetleri (kalem kimliği → yeni adet).</summary>
public sealed record OrderEdit(
    string Address,
    string City,
    string District,
    string Phone,
    string? Note,
    IReadOnlyDictionary<int, int> Quantities);

/// <summary>Müşterinin havale bildirimi; ReceiptFile gizli depoya yazılmış dekontun göreli yolu.</summary>
public sealed record PaymentNoticeDraft(string SenderName, DateTime PaidOn, decimal Amount, string? ReceiptFile);
