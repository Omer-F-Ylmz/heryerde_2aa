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

    private static readonly string[] Brands = ["TAÇ", "Karaca"];

    /// <summary>GÖZ-FIX-2: ad standardı cümle düzeni; marka adları kendi yazımıyla kalır. Yalnız öneri, zorlama yok.</summary>
    [Theory]
    [InlineData("Katlanabilir Mutfak Seti 4'lü", "Katlanabilir mutfak seti 4'lü")]
    [InlineData("Çelik Telli Peynir Kesme Tahtası", "Çelik telli peynir kesme tahtası")]
    [InlineData("TAÇ Çelik Tencere Seti 3'lü", "TAÇ çelik tencere seti 3'lü")]
    // D15 B1: istisna listesi marka tablosundan; "LED" marka olmadığı için artık korunmaz.
    [InlineData("Kristal Görünümlü LED Masa Lambası", "Kristal görünümlü led masa lambası")]
    [InlineData("DÖKÜM TAVA KARACA 28 CM", "Döküm tava Karaca 28 cm")]
    [InlineData("altın işlemeli çatal kaşık takımı", "Altın işlemeli çatal kaşık takımı")]
    [InlineData("İPEK DESENLİ SERVİS TABAĞI", "İpek desenli servis tabağı")]
    [InlineData("  hasır  örgü   saksı sepeti  ", "Hasır örgü saksı sepeti")]
    public void Onerilen_ad_cumle_duzenine_cekilir(string input, string expected)
        => Assert.Equal(expected, ProductRules.NormalizeName(input, Brands));

    [Theory]
    [InlineData("Hasır örgü saksı sepeti")]
    [InlineData("TAÇ çelik tencere seti 3'lü")]
    public void Standarda_uyan_ad_degismez(string name)
        => Assert.Equal(name, ProductRules.NormalizeName(name, Brands));
}
