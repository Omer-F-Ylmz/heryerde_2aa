using HerYerde.Business.Rules;

namespace HerYerde.Tests.Business;

/// <summary>D15 B2: özellik metni. Yönetim formu satır satır, Excel ";" ile yazar; ikisi aynı ayrıştırıcıdan geçer.</summary>
public sealed class AttributeTextTests
{
    [Fact]
    public void Satir_ve_noktali_virgul_ayirir_ilk_iki_noktada_boler_bosluklari_kirpar()
    {
        var (pairs, problem) = AttributeText.Parse(" Malzeme : Çelik; Hacim:3 L\r\nSaat: 12:30 teslim ");

        Assert.Null(problem);
        Assert.Equal(
            [new AttributePair("Malzeme", "Çelik"), new AttributePair("Hacim", "3 L"), new AttributePair("Saat", "12:30 teslim")],
            pairs);
    }

    [Fact]
    public void Degeri_bos_sablon_satiri_atlanir_ayni_ad_ikinci_kez_yazilamaz()
    {
        Assert.Empty(AttributeText.Parse("Malzeme: \nHacim:").Pairs);

        var (pairs, problem) = AttributeText.Parse("Malzeme: Çelik\nmalzeme: Döküm");
        Assert.Empty(pairs);
        Assert.Equal("\"malzeme\" özelliği iki kez yazılmış.", problem);
    }

    [Fact]
    public void Iki_noktasiz_ya_da_uzun_parca_reddedilir()
    {
        Assert.Equal("\"Malzeme Çelik\" satırı \"ad: değer\" biçiminde değil.", AttributeText.Parse("Malzeme Çelik").Problem);
        Assert.Equal("Özellik adı en çok 60, değeri en çok 120 karakter olabilir.", AttributeText.Parse(new string('a', 61) + ": x").Problem);
    }

    [Fact]
    public void Bicimlendirme_ayrac_ile_birlestirir()
        => Assert.Equal(
            "Malzeme:Çelik;Hacim:3 L",
            AttributeText.Format([new AttributePair("Malzeme", "Çelik"), new AttributePair("Hacim", "3 L")], ";"));
}
