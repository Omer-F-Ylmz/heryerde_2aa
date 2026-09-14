using System.Text.RegularExpressions;

namespace HerYerde.Tests.Web;

/// <summary>Bedensiz üründe renk/desen çipleri: 390'da yatay kayar (kenarda gradient ipucu), 768+ sarar.
/// Tarayıcı testi haftalık koştuğu için genişliğe göre geçerli kurallar input.css üzerinden korunur.</summary>
public sealed class ColorChipsCssTests
{
    private static readonly string Css = Regex.Replace(
        RepoFile.ReadAllText("src", "input.css"), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    [Theory]
    [InlineData(768)]
    [InlineData(1440)]
    public void Genis_ekranda_cip_kapsayicisi_sarar_ve_yatay_kaymaz(int width)
    {
        var scroll = Effective(".chips__scroll", width);

        Assert.Equal("wrap", scroll.GetValueOrDefault("flex-wrap"));
        Assert.Equal("visible", scroll.GetValueOrDefault("overflow-x"));
        Assert.Equal("none", scroll.GetValueOrDefault("mask-image"));
    }

    [Fact]
    public void Mobilde_cipler_yatay_kayar_ve_kenarda_gradient_ipucu_verir()
    {
        var scroll = Effective(".chips__scroll", 390);

        Assert.Equal("auto", scroll.GetValueOrDefault("overflow-x"));
        Assert.NotEqual("wrap", scroll.GetValueOrDefault("flex-wrap"));
        Assert.StartsWith("linear-gradient(to right", scroll.GetValueOrDefault("mask-image"));
    }

    /// <summary>Seçicinin verilen genişlikte geçerli bildirimleri: medyasız kurallar, sonra küçükten büyüğe min-width medyaları.</summary>
    private static Dictionary<string, string> Effective(string selector, int width)
    {
        var baseCss = Css;
        foreach (var media in Regex.Matches(Css, @"@media[^{;]+").Select(m => m.Value.Trim()).Distinct())
        {
            foreach (var body in CheckoutCssTests.Rules(Css, media))
            {
                baseCss = baseCss.Replace(body, string.Empty);
            }
        }

        var bodies = CheckoutCssTests.Rules(baseCss, selector).ToList();
        foreach (var min in Regex.Matches(Css, @"@media \(min-width: (\d+)px\) \{").Select(m => int.Parse(m.Groups[1].Value)).Distinct().Where(m => m <= width).Order())
        {
            bodies.AddRange(CheckoutCssTests.Rules(Css, $"@media (min-width: {min}px)").SelectMany(m => CheckoutCssTests.Rules(m, selector)));
        }

        var declared = new Dictionary<string, string>();
        foreach (var declaration in bodies.SelectMany(b => b.Split(';')).Select(d => d.Split(':', 2)).Where(d => d.Length == 2))
        {
            declared[declaration[0].Trim()] = declaration[1].Trim();
        }

        return declared;
    }
}
