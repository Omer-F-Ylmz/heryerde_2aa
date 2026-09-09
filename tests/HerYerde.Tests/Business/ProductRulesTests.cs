using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

public sealed class ProductRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Kampanya_bitisi_gecmisse_etiket_pasiftir()
    {
        var product = new Product { Price = 450m, CampaignPrice = 399m, CampaignLabel = "Çeyiz indirimi", CampaignEndsAt = Now.AddMinutes(-1) };

        Assert.False(ProductRules.CampaignIsActive(product, Now));
    }

    [Fact]
    public void Kampanya_bitisi_gelecekteyse_etiket_aktiftir()
    {
        var product = new Product { Price = 450m, CampaignPrice = 399m, CampaignLabel = "Çeyiz indirimi", CampaignEndsAt = Now.AddDays(1) };

        Assert.True(ProductRules.CampaignIsActive(product, Now));
    }

    [Fact]
    public void Bitis_tarihi_yoksa_kampanya_suresiz_aktiftir()
        => Assert.True(ProductRules.CampaignIsActive(new Product { Price = 450m, CampaignPrice = 399m }, Now));

    [Fact]
    public void Kampanya_fiyati_yoksa_etiket_gosterilmez()
        => Assert.False(ProductRules.CampaignIsActive(new Product { Price = 450m, CampaignEndsAt = Now.AddDays(1) }, Now));

    [Theory]
    [InlineData("giyim", true)]
    [InlineData("ev", false)]
    public void Varyant_zorunlulugu_kok_kategoriye_bagli(string rootSlug, bool expected)
        => Assert.Equal(expected, ProductRules.RequiresVariants(new Category { Slug = rootSlug }));
}
