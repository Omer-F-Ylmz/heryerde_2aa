using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Rules;

public static class ProductRules
{
    /// <summary>Beden/renk yalnız bu kök alanda anlamlı; Ev tarafı varyantsız satılır.</summary>
    public const string VariantRootSlug = "giyim";

    public static bool RequiresVariants(Category root) => root.Slug == VariantRootSlug;

    /// <summary>Kampanya etiketi yalnız indirimli fiyat varken ve bitiş anı geçmemişken gösterilir.</summary>
    public static bool CampaignIsActive(Product product, DateTime now)
        => product.CampaignPrice is not null && (product.CampaignEndsAt is null || product.CampaignEndsAt > now);
}
