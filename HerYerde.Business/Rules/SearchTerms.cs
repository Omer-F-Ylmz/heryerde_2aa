using System.Globalization;
using System.Text.RegularExpressions;

namespace HerYerde.Business.Rules;

/// <summary>Vitrin araması (D15): terimi kelimelere böler, basit Türkçe ek kırpar, eş anlamlılarla genişletir. Kök
/// LIKE '%kök%' ile arandığı için fazla kırpma sonucu yalnız genişletir, ürünü kaçırmaz.</summary>
public static partial class SearchTerms
{
    public const int MaxWords = 5;

    /// <summary>Hal/iyelik eki kırpılınca kalan kök en az bu kadar harf ("kutu" "kut" olmaz); çoğulda üç ("çaylar" → "çay").</summary>
    private const int MinCaseStem = 4;
    private const int MinPluralStem = 3;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Uzundan kısaya; kelime başına biri kırpılır, ardından çoğul eki (tava+lar+ı → tava).</summary>
    private static readonly string[] CaseSuffixes =
    [
        "dan", "den", "tan", "ten",
        "yı", "yi", "yu", "yü", "sı", "si", "su", "sü", "ın", "in", "un", "ün",
        "da", "de", "ta", "te", "ya", "ye",
        "ı", "i", "u", "ü"
    ];

    private static readonly string[] PluralSuffixes = ["lar", "ler"];

    /// <summary>Küçük harf, tek boşluk: günlükte "Granit  TAVA" ile "granit tava" aynı terimdir.</summary>
    public static string Normalize(string term)
        => string.Join(' ', term.ToLower(Turkish).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Stem(string word)
    {
        var stem = word.ToLower(Turkish);
        foreach (var suffix in CaseSuffixes)
        {
            if (stem.EndsWith(suffix, StringComparison.Ordinal) && stem.Length - suffix.Length >= MinCaseStem)
            {
                stem = stem[..^suffix.Length];
                // Ünlüyle başlayan ekte yumuşayan ünsüz geri döner: çaydanlığı → çaydanlık, dolabı → dolap.
                if ("ıiuü".Contains(suffix[0]))
                {
                    stem = stem[..^1] + stem[^1] switch { 'ğ' => 'k', 'b' => 'p', 'c' => 'ç', 'd' => 't', var last => last };
                }

                break;
            }
        }

        foreach (var suffix in PluralSuffixes)
        {
            if (stem.EndsWith(suffix, StringComparison.Ordinal) && stem.Length - suffix.Length >= MinPluralStem)
            {
                return stem[..^suffix.Length];
            }
        }

        return stem;
    }

    /// <summary>Her kelime ayrı grup: önce kökü, sonra kökü eşleşen eş anlamlı grubun kelimeleri. Gruplar VE, grup içi VEYA
    /// ile aranır. Harf/rakam içermeyen terim olduğu gibi tek grup kalır.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Expand(string term, IReadOnlyList<IReadOnlyList<string>> synonyms)
    {
        var words = WordSplitter().Split(term)
            .Where(w => w.Length >= 2)
            .Select(Stem)
            .Distinct()
            .Take(MaxWords)
            .Select(stem => (IReadOnlyList<string>)synonyms
                .Where(group => group.Any(member => Stem(member) == stem))
                .SelectMany(group => group)
                .Prepend(stem)
                .Distinct()
                .ToList())
            .ToList();

        return words.Count > 0 ? words : [[term.Trim()]];
    }

    /// <summary>Sözlük tablosu: başlık ve ayraçtan sonraki her satır virgülle ayrılmış bir grup; tek kelimelik satır yok sayılır.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ParseSynonyms(string markdown)
        => markdown.ReplaceLineEndings("\n").Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('|'))
            .Skip(2)
            .Select(line => (IReadOnlyList<string>)line.Trim('|')
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(word => word.ToLower(Turkish))
                .Distinct()
                .ToList())
            .Where(group => group.Count >= 2)
            .ToList();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex WordSplitter();
}
