using System.Text.RegularExpressions;

namespace HerYerde.Tests.Web;

/// <summary>/odeme yerleşiminin CSS tarafı. Tarayıcı testi yalnız haftalık koştuğu için 390 taşma korumaları ve
/// 768+ statik satırın tek satırlık üç alanı her build'de input.css üzerinden korunur.</summary>
public sealed class CheckoutCssTests
{
    private static readonly string Css = Regex.Replace(
        RepoFile.ReadAllText("src", "input.css"), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    [Fact]
    public void Odeme_390_tasma_korumalari_input_cssde_durur()
    {
        Assert.Contains(Rules(Css, ".checkout"), r => r.Contains("grid-template-columns: minmax(0, 1fr);"));
        Assert.Contains(Rules(Css, ".line__body"), r => r.Contains("min-width: 0;"));
        Assert.Contains(Rules(Css, ".iban"), r => r.Contains("flex-wrap: wrap;"));
    }

    [Fact]
    public void Statik_satir_768_ve_ustunde_uc_alanli_tek_satir_kalir()
    {
        var tablet = Rules(Css, "@media (min-width: 768px)").ToList();

        Assert.Contains(tablet.SelectMany(m => Rules(m, ".line--static")), r => r.Contains("grid-template-areas: \"media body total\";"));
        var title = Assert.Single(tablet.SelectMany(m => Rules(m, ".line__title--single a")));
        Assert.Contains("white-space: nowrap;", title);
        Assert.Contains("overflow: hidden;", title);
        Assert.Contains("text-overflow: ellipsis;", title);
    }

    /// <summary>Başlığı tam olarak <paramref name="header"/> olan blokların gövdeleri; iç içe bloklarda da arar.</summary>
    private static IEnumerable<string> Rules(string css, string header)
    {
        for (var open = css.IndexOf('{'); open > 0; open = css.IndexOf('{', open + 1))
        {
            var start = css.LastIndexOfAny(['{', '}', ';'], open - 1) + 1;
            if (css[start..open].Trim() != header)
            {
                continue;
            }

            var depth = 0;
            var close = open;
            do
            {
                depth += css[close] switch { '{' => 1, '}' => -1, _ => 0 };
                close++;
            } while (depth > 0);

            yield return css[(open + 1)..(close - 1)];
        }
    }
}
