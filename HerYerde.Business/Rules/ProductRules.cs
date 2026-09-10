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

    /// <summary>Kampanya etiketi yalnız indirimli fiyat varken ve bitiş anı geçmemişken gösterilir.</summary>
    public static bool CampaignIsActive(Product product, DateTime now)
        => product.CampaignPrice is not null && (product.CampaignEndsAt is null || product.CampaignEndsAt > now);
}
