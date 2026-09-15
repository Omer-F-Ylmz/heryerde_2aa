using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Değişmiş slug: eski adres 301 ile kaydın bugünkü adresine gider. (EntityType, OldSlug) tekildir; aynı eski slug
/// yeniden kaydedilirse satır son sahibine döner.</summary>
public class SlugHistory : IEntity
{
    public int Id { get; set; }

    /// <summary>Bkz. <see cref="SlugEntity"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }
    public string OldSlug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public static class SlugEntity
{
    public const string Product = "urun";
    public const string Category = "kategori";
}
