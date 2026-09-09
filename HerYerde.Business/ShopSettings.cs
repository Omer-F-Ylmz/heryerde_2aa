namespace HerYerde.Business;

/// <summary>appsettings "Shop" bölümü; kargo ücreti ve havale hesabı buradan gelir.</summary>
public sealed class ShopSettings
{
    public decimal ShippingFee { get; set; }
    public string Iban { get; set; } = string.Empty;
}
