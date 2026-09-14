namespace HerYerde.Business;

public sealed class ShippingSettings
{
    public CarrierSettings[] Carriers { get; set; } = [];

    /// <summary>Takip sayfasının adresi; firma tanımsız ya da şablon boşsa bağlantı verilmez.</summary>
    public string? TrackingUrl(string? carrier, string? trackingNo)
    {
        if (string.IsNullOrWhiteSpace(carrier) || string.IsNullOrWhiteSpace(trackingNo))
        {
            return null;
        }

        var template = Carriers
            .FirstOrDefault(c => string.Equals(c.Name, carrier, StringComparison.OrdinalIgnoreCase))?
            .TrackingUrl;

        return string.IsNullOrWhiteSpace(template) ? null : template.Replace("{0}", Uri.EscapeDataString(trackingNo));
    }

    public bool Knows(string? carrier) => !string.IsNullOrWhiteSpace(carrier)
        && Carriers.Any(c => string.Equals(c.Name, carrier, StringComparison.OrdinalIgnoreCase));
}

public sealed class CarrierSettings
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Takip adresi şablonu; "{0}" takip numarasıyla değişir.</summary>
    public string TrackingUrl { get; set; } = string.Empty;
}
