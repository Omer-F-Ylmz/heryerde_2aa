namespace HerYerde.Web.Models;

/// <summary>Ürün akışlarının sabitleri: marka adı ve kategori slug'ından Google ürün taksonomisine eşleme.
/// Eşlenmemiş kategori genel ev başlığına düşer; Merchant bilinmeyen dizgede kaydı reddeder.</summary>
public static class FeedCatalog
{
    public const string Brand = "HerYerde";

    /// <summary>Taksonomideki tam yol (Google hem numarayı hem tam yolu kabul eder).</summary>
    private const string HomeDefault = "Home & Garden";

    private static readonly Dictionary<string, string> Categories = new(StringComparer.Ordinal)
    {
        ["ev"] = "Home & Garden > Kitchen & Dining",
        ["mutfak-sofra"] = "Home & Garden > Kitchen & Dining > Kitchen Tools & Utensils",
        ["saklama"] = "Home & Garden > Kitchen & Dining > Food Storage",
        ["sepet"] = "Home & Garden > Decor > Baskets",
        ["kucuk-ev-aleti"] = "Home & Garden > Kitchen & Dining > Kitchen Appliances",
        ["dekor"] = "Home & Garden > Decor",
        ["giyim"] = "Apparel & Accessories > Clothing Accessories > Scarves & Shawls",
        ["esarp"] = "Apparel & Accessories > Clothing Accessories > Scarves & Shawls",
        ["salvar"] = "Apparel & Accessories > Clothing > Pants"
    };

    public static string Category(string? categorySlug)
        => categorySlug is { Length: > 0 } slug && Categories.TryGetValue(slug, out var category) ? category : HomeDefault;
}
