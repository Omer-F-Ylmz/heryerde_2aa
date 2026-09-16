using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Vitrin araması (D15): normalleştirilmiş terim, sonuç sayısı, zaman. Ziyaretçiye bağlanan hiçbir alan (IP, çerez,
/// oturum) tutulmaz; kişisel veri değildir (docs/veri-envanteri.md).</summary>
public class SearchLog : IEntity
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
