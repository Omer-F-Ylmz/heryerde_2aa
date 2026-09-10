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
    bool StockTracked)
{
    public decimal LineTotal => UnitPrice * Quantity;

    /// <summary>Stok takipli üründe stok satırdaki adedi karşılamıyor; ödeme adımında 409 döner.</summary>
    public bool Insufficient => StockTracked && AvailableStock < Quantity;
}

/// <summary>"1 alana 1 hediye" satırı: sepette gösterilmez, ödeme özetinde ve siparişte 0 ₺ görünür.</summary>
public sealed record CartGift(string ProductName, int Quantity);

public sealed record CartView(
    Guid CartId,
    IReadOnlyList<CartLine> Lines,
    decimal Subtotal,
    decimal ShippingFee,
    IReadOnlyList<CartGift>? Gifts = null,
    string? GiftNote = null)
{
    public decimal Total => Subtotal + ShippingFee;
    public int Count => Lines.Sum(l => l.Quantity);
    public bool IsEmpty => Lines.Count == 0;
    public IReadOnlyList<CartGift> GiftLines => Gifts ?? [];
}
