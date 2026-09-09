using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Anonim sepet; kimliği "heryerde.cart" çerezinde taşınır, kullanıcı hesabı yok.</summary>
public class Cart : IEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
}
