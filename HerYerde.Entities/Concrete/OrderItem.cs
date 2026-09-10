using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Sipariş anındaki kopya; ürün sonradan değişse de satır sabit kalır.</summary>
public class OrderItem : IEntity
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Kampanya hediyesi satırı; birim fiyatı 0 ve tutara girmez.</summary>
    public bool IsGift { get; set; }
}
