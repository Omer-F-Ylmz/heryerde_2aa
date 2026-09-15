using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Dtos;

/// <summary>Talep satırı: sipariş kalemi, adet; değişimde istenen varyantın stok kodu.</summary>
public sealed record ReturnLine(int OrderItemId, int Quantity, string? NewSku = null);

/// <summary>Müşterinin talep formu; PhotoFile gizli depoya yazılmış fotoğrafın göreli yolu.</summary>
public sealed record ReturnDraft(ReturnType Type, string Reason, IReadOnlyList<ReturnLine> Lines, string? Iban, string? PhotoFile);

/// <summary>Talep formunun satırı: kalem, hâlâ talep edilebilir adet, değişimde seçilebilecek stoktaki diğer varyantlar.</summary>
public sealed record ReturnFormLine(OrderItem Item, int Returnable, IReadOnlyList<ProductVariant> Alternatives);

/// <summary>Müşterinin talep formu; NeedsIban kartla ödenmemiş siparişte iade için IBAN istenir.</summary>
public sealed record ReturnForm(Order Order, IReadOnlyList<ReturnFormLine> Lines, bool NeedsIban);

/// <summary>Talep, siparişi ve kalemleri (sipariş kalemiyle eşleşmiş).</summary>
public sealed record ReturnDetail(ReturnRequest Request, Order Order, IReadOnlyList<(ReturnRequestItem Item, OrderItem OrderItem)> Items);

/// <summary>Yönetim listesi satırı; RefundDaysLeft yalnız geri ödemesi bekleyen iadede dolu (eksi: süre geçti).</summary>
public sealed record ReturnListItem(ReturnRequest Request, string OrderNo, string FullName, int? RefundDaysLeft);
