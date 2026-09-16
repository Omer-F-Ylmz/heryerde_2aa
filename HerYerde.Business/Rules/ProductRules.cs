using System.Globalization;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Rules;

public static class ProductRules
{
    /// <summary>Beden/renk yalnız bu kök alanda anlamlı; Ev tarafı varyantsız satılır.</summary>
    public const string VariantRootSlug = "giyim";

    public static bool RequiresVariants(Category root) => root.Slug == VariantRootSlug;

    /// <summary>Görsel adresi ya http/https mutlak adres ya da tek "/" ile başlayan yerel yoldur.
    /// javascript:, data:, vbscript: ve protokolsuz "//host" reddedilir.</summary>
    public static bool IsAllowedImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.StartsWith('/'))
        {
            return true;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>İthalden gelen ürün fiyatsız kaydedilir (price=1); bu bayrakla yayına alınamaz.</summary>
    public const decimal PriceMissingThreshold = 1m;

    public static bool PriceMissing(Product product) => product.Price <= PriceMissingThreshold;

    /// <summary>Kampanya etiketi yalnız indirimli fiyat varken ve bitiş anı geçmemişken gösterilir.</summary>
    public static bool CampaignIsActive(Product product, DateTime now)
        => product.CampaignPrice is not null && (product.CampaignEndsAt is null || product.CampaignEndsAt > now);

    /// <summary>Ad standardında büyük kalan marka ve kısaltmalar; listede olmayan her sözcük küçültülür.</summary>

    /// <summary>Önerilen yazım: cümle düzeni (ilk harf büyük, gerisi küçük); marka tablosundaki adların kelimeleri
    /// kendi yazımıyla kalır (ör. "TAÇ", "Karaca").
    /// Yalnız öneridir — hiçbir yerde doğrulama olarak uygulanmaz.</summary>
    public static string NormalizeName(string? name, IEnumerable<string> brandNames)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        // Marka adındaki her kelime kendi yazımıyla korunur ("TAÇ", "Karaca"): büyük/küçük harf farkı gözetmeden eşlenir.
        var brandWords = brandNames
            .SelectMany(b => b.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .GroupBy(w => w.ToUpper(turkish))
            .ToDictionary(g => g.Key, g => g.First());
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < words.Length; i++)
        {
            if (brandWords.TryGetValue(words[i].ToUpper(turkish), out var brandWord))
            {
                words[i] = brandWord;
                continue;
            }

            var lower = words[i].ToLower(turkish);
            words[i] = i == 0 ? string.Concat(lower[..1].ToUpper(turkish), lower[1..]) : lower;
        }

        return string.Join(' ', words);
    }
}
