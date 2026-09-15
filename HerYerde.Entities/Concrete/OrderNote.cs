using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Siparişe yöneticinin düştüğü iç not; müşteri görmez, zaman çizelgesinde yer alır.</summary>
public class OrderNote : IEntity
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int AdminId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
