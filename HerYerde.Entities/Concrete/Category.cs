using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>İki seviye: kök (Giyim | Ev) ve onun altı. Alt kategorinin altına kategori açılmaz.</summary>
public class Category : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
