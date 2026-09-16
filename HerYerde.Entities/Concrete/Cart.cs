using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Anonim sepet; kimliği "heryerde.cart" çerezinde taşınır, kullanıcı hesabı yok.</summary>
public class Cart : IEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Uygulanan kupon kodu; sipariş anında yeniden doğrulanır (arada süresi dolmuş olabilir).</summary>
    public string? CouponCode { get; set; }
}
