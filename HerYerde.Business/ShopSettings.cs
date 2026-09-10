namespace HerYerde.Business;

public sealed class ShopSettings
{
    public decimal ShippingFee { get; set; }
    public string Iban { get; set; } = string.Empty;

    /// <summary>Sepet satırı başına adet tavanı; perakende bir siparişin makul üst sınırı.</summary>
    public int MaxQtyPerLine { get; set; } = 10;
}
