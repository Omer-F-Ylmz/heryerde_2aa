using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

public class ProductImage : IEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Url { get; set; } = string.Empty;

    /// <summary>Ekran okuyucu ve görsel yüklenmediğinde gösterilecek metin.</summary>
    public string Alt { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}
