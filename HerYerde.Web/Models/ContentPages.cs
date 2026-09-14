namespace HerYerde.Web.Models;

public sealed record ContentPage(string Path, string Title);

/// <summary>Kurumsal sayfalar: altbilgi Kurumsal sütunu ve sitemap aynı listeden okur.</summary>
public static class ContentPages
{
    /// <summary>Sitemap lastmod; metinler değişince güncellenir.</summary>
    public static readonly DateTime UpdatedAt = new(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyList<ContentPage> All =
    [
        new("/hakkimizda", "Hakkımızda"),
        new("/iletisim", "İletişim"),
        new("/sss", "Sık sorulan sorular")
    ];
}
