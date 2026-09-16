using HerYerde.Business.Rules;

namespace HerYerde.Business.Dtos;

/// <summary>Sepet satırı; fiyat sepete atıldığı andan taşınır, stok anlık okunur.</summary>
public sealed record CartLine(
    int ItemId,
    int ProductId,
    string ProductName,
    string Slug,
    string? ImageUrl,
    string? Size,
    string? Color,
    int Quantity,
    decimal UnitPrice,
    int AvailableStock,
    bool StockTracked,
    string? RegistryOwner = null)
{
    public decimal LineTotal => UnitPrice * Quantity;

    /// <summary>Stok takipli üründe stok satırdaki adedi karşılamıyor; ödeme adımında 409 döner.</summary>
    public bool Insufficient => StockTracked && AvailableStock < Quantity;
}

/// <summary>"1 alana 1 hediye" satırı: sepette gösterilmez, ödeme özetinde ve siparişte 0 ₺ görünür.</summary>
public sealed record CartGift(string ProductName, int Quantity);

/// <summary>Kupon değerlendirmesi. Problem doluysa kupon geçmez ve indirim 0'dır.</summary>
public sealed record CouponView(string? Code, decimal Discount, bool FreeShipping, string? Problem = null)
{
    public static readonly CouponView None = new(null, 0m, false);

    public bool Valid => Problem is null && Code is not null;
}

public sealed record CartView(
    Guid CartId,
    IReadOnlyList<CartLine> Lines,
    decimal Subtotal,
    decimal ShippingFee,
    IReadOnlyList<CartGift>? Gifts = null,
    string? GiftNote = null,
    decimal FreeShippingOver = 0m,
    string? CouponCode = null,
    decimal Discount = 0m,
    string? CouponProblem = null)
{
    public decimal Total => Subtotal + ShippingFee - Discount;
    public int Count => Lines.Sum(l => l.Quantity);
    public bool IsEmpty => Lines.Count == 0;
    public IReadOnlyList<CartGift> GiftLines => Gifts ?? [];

    public bool FreeShipping => ShippingRules.IsFree(Subtotal, FreeShippingOver);

    /// <summary>Bedava kargoya kalan tutar; eşik kapalıysa ya da aşıldıysa 0.</summary>
    public decimal ToFreeShipping => ShippingRules.Remaining(Subtotal, FreeShippingOver);
}
