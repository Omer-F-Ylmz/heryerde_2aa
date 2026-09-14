using HerYerde.Web.Infrastructure;

namespace HerYerde.Web.Models;

/// <summary>Kayıtlı adres 800'lüktür; diğer boyutlar adresten türetilir, ayrı alan tutulmaz.</summary>
public static class ProductImages
{
    private const string PrimarySuffix = "-800.webp";

    public const string CardSizes = "(min-width: 960px) 300px, (min-width: 640px) 45vw, 92vw";

    public const string GallerySizes = "(min-width: 960px) 560px, 92vw";

    public const string ThumbSizes = "80px";

    /// <summary>Depoya yüklenmemiş adreslerde (eski kayıt, dış CDN) null döner; işaretleme tek boyutta kalır.</summary>
    public static string? Srcset(string? url)
    {
        if (url is null || !url.EndsWith(PrimarySuffix, StringComparison.Ordinal))
        {
            return null;
        }

        var stem = url[..^PrimarySuffix.Length];
        return string.Join(", ", ProductImageStorage.Widths.Select(width => $"{stem}-{width}.webp {width}w"));
    }
}
