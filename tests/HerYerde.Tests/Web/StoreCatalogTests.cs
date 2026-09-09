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
    public void Gecmis_kampanya_hero_ya_cikmaz_en_yakin_biten_secilir()
    {
        var products = new[]
        {
            P(1, 1000, 800, Now.AddDays(-1)),
            P(2, 1000, 800, Now.AddDays(5)),
            P(3, 1000, 800, Now.AddDays(2)),
            P(4, 1000, 800)
        };

        var hero = StoreCatalog.PickHero(products, Now);

        Assert.NotNull(hero);
        Assert.Equal(3, hero.Id);
    }

    [Fact]
    public void Suresi_dolan_kampanya_aktif_sayilmaz_ve_rozet_dusmez()
    {
        Assert.False(StoreCatalog.IsCampaignActive(P(1, 1000, 800, Now.AddSeconds(-1)), Now));
        Assert.True(StoreCatalog.IsCampaignActive(P(2, 1000, 800, Now.AddSeconds(1)), Now));
        Assert.True(StoreCatalog.IsCampaignActive(P(3, 1000, 800), Now));
        Assert.False(StoreCatalog.IsCampaignActive(P(4, 1000), Now));
    }

    [Fact]
    public void Fiyat_siralamasi_kampanyali_fiyati_esas_alir_yeni_siralamasi_tarihe_gore()
    {
        var products = new[] { P(1, 900), P(2, 1000, 500, Now.AddDays(1)), P(3, 700) };

        var byPrice = StoreCatalog.Sort(products, "fiyat", Now).Select(p => p.Id).ToList();
        var byNew = StoreCatalog.Sort(products, "yeni", Now).Select(p => p.Id).ToList();

        Assert.Equal([2, 3, 1], byPrice);
        Assert.Equal([1, 2, 3], byNew);
    }

    [Fact]
    public void Benzer_urunler_ayni_alt_kategoriden_kendisi_haric_en_fazla_dort()
    {
        var self = P(1, 100, categoryId: 7);
        var products = new[] { self, P(2, 100, categoryId: 7), P(3, 100, categoryId: 7), P(4, 100, categoryId: 8), P(5, 100, categoryId: 7), P(6, 100, categoryId: 7), P(7, 100, categoryId: 7, active: false) };

        var similar = StoreCatalog.Similar(products, self).Select(p => p.Id).ToList();

        Assert.Equal(4, similar.Count);
        Assert.DoesNotContain(1, similar);
        Assert.DoesNotContain(4, similar);
        Assert.DoesNotContain(7, similar);
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
