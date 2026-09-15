namespace HerYerde.Business;

public sealed class ShopSettings
{
    public decimal ShippingFee { get; set; }

    /// <summary>Bu ara toplamdan itibaren kargo bedava; 0 ise eşik kapalıdır.</summary>
    public decimal FreeShippingOver { get; set; }
    public string Iban { get; set; } = string.Empty;

    /// <summary>Sepet satırı başına adet tavanı; perakende bir siparişin makul üst sınırı.</summary>
    public int MaxQtyPerLine { get; set; } = 10;

    /// <summary>Canonical, OG, sitemap ve JSON-LD'deki mutlak adreslerin kökü (sonda / yok).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Ürün görselinin gelebileceği dış kökenler (ör. https://placehold.co); CSP img-src'e eklenir.</summary>
    public string[] ImageOrigins { get; set; } = [];

    /// <summary>Ürün sayfasında "Son N adet" rozeti bu adet ve altında görünür (varyantlıda seçili varyanta göre).</summary>
    public int LowStockBadgeAt { get; set; } = 3;

    /// <summary>Yönetimde düşük stok sayacı ve /admin/stok listesi bu adet ve altını sayar.</summary>
    public int LowStockAlertAt { get; set; } = 5;
}
