namespace HerYerde.Business;

/// <summary>İyzico sanal POS ayarı. Anahtarlar ortam değişkeninden gelir; boşken kartla ödeme kapalıdır.</summary>
public sealed class IyzicoSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://sandbox-api.iyzipay.com";

    /// <summary>3D doğrulama formunun gidebileceği kökenler; yalnız ödeme sayfalarında CSP form-action ve frame-src'e eklenir.</summary>
    public string[] CspSources { get; set; } = [];

    public bool IsConfigured => ApiKey.Length > 0 && SecretKey.Length > 0;
}
