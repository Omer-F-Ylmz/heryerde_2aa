using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Giyin tarafı beden/renk ile varyantlı; ev ürünlerinin çoğunda ikisi de boş kalır.</summary>
public class ProductVariant : IEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Stock { get; set; }

    /// <summary>SQL Server rowversion; yönetici stok kaydı ile satıştaki düşümün çakışmasını yakalar.</summary>
    public byte[] RowVersion { get; set; } = [];
}
