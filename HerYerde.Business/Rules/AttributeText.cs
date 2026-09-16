namespace HerYerde.Business.Rules;

public sealed record AttributePair(string Name, string Value);

/// <summary>Özelliklerin metin biçimi: "ad: değer" parçaları; yönetim formunda satır satır, Excel'de ";" ile.</summary>
public static class AttributeText
{
    public const int NameLength = 60;
    public const int ValueLength = 120;

    /// <summary>";" ya da satır sonu ayırır, ilk ":" ad ile değeri böler. Değeri boş parça (şablon satırı) atlanır.
    /// Hatalı parça varsa hiçbir çift dönmez.</summary>
    public static (IReadOnlyList<AttributePair> Pairs, string? Problem) Parse(string? text)
    {
        var pairs = new List<AttributePair>();
        var names = new HashSet<string>(StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true));
        foreach (var part in (text ?? string.Empty).Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0)
            {
                return ([], $"\"{part}\" satırı \"ad: değer\" biçiminde değil.");
            }

            var name = part[..colon].Trim();
            var value = part[(colon + 1)..].Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (name.Length > NameLength || value.Length > ValueLength)
            {
                return ([], $"Özellik adı en çok {NameLength}, değeri en çok {ValueLength} karakter olabilir.");
            }

            if (!names.Add(name))
            {
                return ([], $"\"{name}\" özelliği iki kez yazılmış.");
            }

            pairs.Add(new AttributePair(name, value));
        }

        return (pairs, null);
    }

    public static string Format(IEnumerable<AttributePair> pairs, string separator)
        => string.Join(separator, pairs.Select(p => p.Name + ":" + p.Value));
}
