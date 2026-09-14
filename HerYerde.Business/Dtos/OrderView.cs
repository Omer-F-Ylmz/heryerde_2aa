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

/// <summary>Payment yalnız kartla ödenen siparişte dolu.</summary>
public sealed record OrderDetail(Order Order, IReadOnlyList<OrderItem> Items, Payment? Payment = null);
