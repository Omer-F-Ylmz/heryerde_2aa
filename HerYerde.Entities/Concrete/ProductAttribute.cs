using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Ürün özelliği (ör. Malzeme: Çelik). Ürün sayfasında tablo, listelemede süzgeç olur.</summary>
public class ProductAttribute : IEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
