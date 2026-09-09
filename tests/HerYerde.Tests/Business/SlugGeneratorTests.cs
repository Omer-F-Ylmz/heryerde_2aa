using HerYerde.Business.Utilities;

namespace HerYerde.Tests.Business;

public sealed class SlugGeneratorTests
{
    [Theory]
    [InlineData("Şile Bezi Şalvar", "sile-bezi-salvar")]
    [InlineData("Mutfak & Sofra", "mutfak-sofra")]
    [InlineData("  Çelik Tencere 24'lük  ", "celik-tencere-24-luk")]
    public void Turkce_baslik_ascii_sluga_cevrilir(string text, string expected)
        => Assert.Equal(expected, SlugGenerator.Generate(text));

    [Fact]
    public async Task Slug_aliniysa_sonuna_2_eklenir_o_da_aliniysa_3_olur()
    {
        var taken = new HashSet<string> { "salvar", "salvar-2" };

        var slug = await SlugGenerator.MakeUniqueAsync("salvar", candidate => Task.FromResult(taken.Contains(candidate)));

        Assert.Equal("salvar-3", slug);
    }
}
