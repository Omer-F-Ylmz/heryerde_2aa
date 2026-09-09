using System.Text;

namespace HerYerde.Business.Utilities;

public static class SlugGenerator
{
    private static readonly Dictionary<char, char> TurkishToAscii = new()
    {
        ['ç'] = 'c',
        ['Ç'] = 'c',
        ['ğ'] = 'g',
        ['Ğ'] = 'g',
        ['ı'] = 'i',
        ['İ'] = 'i',
        ['ö'] = 'o',
        ['Ö'] = 'o',
        ['ş'] = 's',
        ['Ş'] = 's',
        ['ü'] = 'u',
        ['Ü'] = 'u'
    };

    /// <summary>Başlığı ASCII slug'a çevirir: TR harfleri karşılığına düşer, diğer her şey tek "-" olur.</summary>
    public static string Generate(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (TurkishToAscii.TryGetValue(character, out var ascii))
            {
                builder.Append(ascii);
            }
            else if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>Slug alınmışsa "-2", "-3" … ekleyerek ilk boş olanı döndürür.</summary>
    public static async Task<string> MakeUniqueAsync(string baseSlug, Func<string, Task<bool>> isTaken)
    {
        var candidate = baseSlug;
        var suffix = 1;
        while (await isTaken(candidate))
        {
            suffix++;
            candidate = $"{baseSlug}-{suffix}";
        }

        return candidate;
    }
}
