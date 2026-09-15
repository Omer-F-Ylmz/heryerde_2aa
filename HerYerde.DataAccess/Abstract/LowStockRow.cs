namespace HerYerde.DataAccess.Abstract;

/// <summary>Düşük stok satırı: varyantsa Sku/Size/Color dolu, varyantsız üründe boş.</summary>
public sealed record LowStockRow(int ProductId, string ProductName, string? Sku, string? Size, string? Color, int Stock);
