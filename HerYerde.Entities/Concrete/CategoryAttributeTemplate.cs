using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Kategorideki ürünlerin doldurması beklenen özellik adı; ürün formunda boş satır olarak önerilir.</summary>
public class CategoryAttributeTemplate : IEntity
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
