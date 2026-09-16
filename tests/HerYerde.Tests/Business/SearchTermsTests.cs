using HerYerde.Business.Rules;

namespace HerYerde.Tests.Business;

/// <summary>D15 B4: Türkçe ek toleransı (basit ek kırpma) ve eş anlamlı genişletme; DB'siz.</summary>
public sealed class SearchTermsTests
{
    [Theory]
    [InlineData("tencereler", "tencere")]
    [InlineData("tavaları", "tava")]
    [InlineData("örtüsü", "örtü")]
    [InlineData("çaydanlığı", "çaydanlık")]
    [InlineData("dolabı", "dolap")]
    [InlineData("sepette", "sepet")]
    [InlineData("çaylar", "çay")]
    [InlineData("TENCERELER", "tencere")]
    [InlineData("kutu", "kutu")]
    [InlineData("ev", "ev")]
    public void Cogul_ve_hal_ekleri_kirpilir_kisa_kok_korunur(string word, string stem)
        => Assert.Equal(stem, SearchTerms.Stem(word));

    [Fact]
    public void Sozluk_tablosu_satir_basina_bir_es_anlamli_grubu_verir()
    {
        var groups = SearchTerms.ParseSynonyms("""
            # Eş anlamlılar

            | Kelimeler |
            |---|
            | sürahi, karaf |
            | nevresim, yatak takımı |
            | tek |
            """);

        Assert.Equal(2, groups.Count);
        Assert.Equal(["sürahi", "karaf"], groups[0]);
        Assert.Equal(["nevresim", "yatak takımı"], groups[1]);
    }

    [Fact]
    public void Her_kelime_koku_ve_es_anlamlilariyla_ayri_grup_olur()
    {
        IReadOnlyList<IReadOnlyList<string>> synonyms = [["sürahi", "karaf"], ["nevresim", "yatak takımı"]];

        var words = SearchTerms.Expand("Karaflar  cam, nevresim", synonyms);

        Assert.Equal(3, words.Count);
        Assert.Equal(["karaf", "sürahi"], words[0]);
        Assert.Equal(["cam"], words[1]);
        Assert.Equal(["nevresim", "yatak takımı"], words[2]);
    }

    [Fact]
    public void Kelimesi_kalmayan_terim_oldugu_gibi_aranir()
        => Assert.Equal([["%%"]], SearchTerms.Expand("%%", []));
}
