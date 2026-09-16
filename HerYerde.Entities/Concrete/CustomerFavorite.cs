using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Üyenin favori ürünü; (üye, ürün) tekil.</summary>
public class CustomerFavorite : IEntity
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public DateTime CreatedAt { get; set; }
}
