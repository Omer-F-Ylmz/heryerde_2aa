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

    /// <summary>Siparişin en geç kaç iş gününde kargoya verildiği; ürün sayfası ve sepette yazar, panoda gecikme uyarısının eşiği.</summary>
    public int DispatchDays { get; set; } = 2;

    /// <summary>Onaylanan iade/değişim ürününün gönderileceği adres; onay postasında yazar.</summary>
    public string ReturnAddress { get; set; } = string.Empty;

    /// <summary>İade gönderisinin kargo bilgisi (firma, anlaşma kodu); onay postasında yazar.</summary>
    public string ReturnCarrier { get; set; } = string.Empty;
}
