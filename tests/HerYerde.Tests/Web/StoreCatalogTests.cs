using HerYerde.Entities.Concrete;
using HerYerde.Web.Models;

namespace HerYerde.Tests.Web;

/// <summary>Vitrin seçim kuralları: DB'siz, saf fonksiyon.</summary>
public sealed class StoreCatalogTests
{
    private static readonly DateTime Now = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    private static Product P(int id, decimal price, decimal? campaign = null, DateTime? endsAt = null, int categoryId = 1, bool active = true) => new()
    {
        Id = id,
        Name = "Ürün " + id,
        Slug = "urun-" + id,
        Price = price,
        CampaignPrice = campaign,
        CampaignEndsAt = endsAt,
        CategoryId = categoryId,
        IsActive = active,
        CreatedAt = Now.AddDays(-id)
    };

    [Fact]
    public void Suresi_dolan_kampanya_aktif_sayilmaz_ve_rozet_dusmez()
    {
        Assert.False(StoreCatalog.IsCampaignActive(P(1, 1000, 800, Now.AddSeconds(-1)), Now));
        Assert.True(StoreCatalog.IsCampaignActive(P(2, 1000, 800, Now.AddSeconds(1)), Now));
        Assert.True(StoreCatalog.IsCampaignActive(P(3, 1000, 800), Now));
        Assert.False(StoreCatalog.IsCampaignActive(P(4, 1000), Now));
    }

    [Fact]
    public void Varyant_secici_beden_renk_kimlik_ucluesunu_secenek_olarak_tasir()
    {
        var picker = StoreCatalog.Picker(new[]
        {
            new ProductVariant { Id = 11, ProductId = 1, Size = "M", Color = "Kiremit", Sku = "SLV-M-K", Stock = 3 },
            new ProductVariant { Id = 12, ProductId = 1, Size = "L", Color = "Kiremit", Sku = "SLV-L-K", Stock = 0 }
        });

        Assert.Equal(2, picker.Options!.Count);
        var medium = picker.Options.Single(o => o.Id == 11);
        Assert.Equal("M", medium.Size);
        Assert.Equal("Kiremit", medium.Color);
        Assert.Equal(3, medium.Stock);
        Assert.Equal(0, picker.Options.Single(o => o.Id == 12).Stock);
    }

    [Fact]
    public void Whatsapp_linki_urun_adini_url_encoded_tasir()
    {
        var url = StoreCatalog.WhatsAppUrl("https://wa.me/905000000000", "Granit döküm tencere seti");

        Assert.StartsWith("https://wa.me/905000000000?text=", url);
        Assert.Contains("Granit%20d%C3%B6k%C3%BCm%20tencere%20seti", url);
        Assert.DoesNotContain(" ", url);
    }
}
