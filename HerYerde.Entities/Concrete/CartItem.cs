using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

public class CartItem : IEntity
{
    public int Id { get; set; }
    public Guid CartId { get; set; }
    public int ProductId { get; set; }

    /// <summary>Varyantsız (Ev) üründe boş kalır.</summary>
    public int? VariantId { get; set; }
    public int Quantity { get; set; }

    /// <summary>Sepete atıldığı andaki fiyat; kampanya aktifse kampanya fiyatı.</summary>
    public decimal UnitPrice { get; set; }
}
