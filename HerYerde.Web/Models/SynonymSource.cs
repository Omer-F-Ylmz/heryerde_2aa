using HerYerde.Business.Rules;

namespace HerYerde.Web.Models;

/// <summary>Eş anlamlı sözlüğü: docs/arama-esanlam.md (çıktıya bağlantılı kopya) ilk aramada bir kez okunur; değişiklik
/// yeniden başlatmayla gelir.</summary>
public static class SynonymSource
{
    private static readonly Lazy<IReadOnlyList<IReadOnlyList<string>>> Groups = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "arama-esanlam.md");
        return File.Exists(path) ? SearchTerms.ParseSynonyms(File.ReadAllText(path)) : [];
    });

    public static IReadOnlyList<IReadOnlyList<string>> Load() => Groups.Value;
}
