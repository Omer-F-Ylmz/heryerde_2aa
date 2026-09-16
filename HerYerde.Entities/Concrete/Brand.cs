using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Ürün markası. Adın yazımı ürün adı önerisinde de esas alınır (ör. "TAÇ" büyük kalır).</summary>
public class Brand : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>/marka/{slug}.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>İsteğe bağlı logo; yönetimden yüklenir, depo biçiminde adres.</summary>
    public string? LogoUrl { get; set; }
}
