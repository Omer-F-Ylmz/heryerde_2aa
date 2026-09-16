using HerYerde.DataAccess.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Models;

/// <summary>Ziyaretçinin listeleme süzgeçleri; sorgu dizesinden bağlanır. oz değeri "Ad:Değer" (ilk iki nokta böler),
/// stok/kampanya/sayim "1" ile açılır. sayim=1 sayfa yerine yalnız sonuç sayısını ister (canlı sayaç).</summary>
public sealed class ListingSelection
{
    /// <summary>Uydurulmuş uzun bağlantı sorguyu şişirmesin: süzgeç türü başına üst sınır.</summary>
    private const int MaxValues = 20;

    [FromQuery(Name = "marka")]
    public string[] Brands { get; set; } = [];

    [FromQuery(Name = "oz")]
    public string[] Attributes { get; set; } = [];

    [FromQuery(Name = "stok")]
    public string? InStock { get; set; }

    [FromQuery(Name = "kampanya")]
    public string? Campaign { get; set; }

    [FromQuery(Name = "sayim")]
    public string? Count { get; set; }

    public bool InStockOnly => InStock == "1";

    public bool CampaignOnly => Campaign == "1";

    public bool CountOnly => Count == "1";

    public IReadOnlyList<string> BrandSlugs => Clean(Brands);

    public IReadOnlyList<(string Name, string Value)> AttributePairs => Clean(Attributes)
        .Select(v => v.Split(':', 2, StringSplitOptions.TrimEntries))
        .Where(p => p.Length == 2 && p[0].Length > 0 && p[1].Length > 0)
        .Select(p => (p[0], p[1]))
        .ToList();

    public IReadOnlyList<AttributeFilter> AttributeFilters => AttributePairs
        .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .Select(g => new AttributeFilter(g.Key, g.Select(p => p.Value).ToList()))
        .ToList();

    public int ActiveCount => BrandSlugs.Count + AttributePairs.Count + (InStockOnly ? 1 : 0) + (CampaignOnly ? 1 : 0);

    public bool HasAttribute(string name, string value)
        => AttributePairs.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.Value == value);

    private static List<string> Clean(IEnumerable<string> values)
        => values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().Take(MaxValues).ToList();
}

/// <summary>Listelemenin tam durumu; sıralama, sayfa, çip ve temizle bağlantıları hep buradan aynı sırayla üretilir:
/// q, sirala, min, max, marka…, oz…, stok, kampanya.</summary>
public sealed record ListingState(string Path, string? Term, bool ByPrice, decimal? Min, decimal? Max, ListingSelection Selection)
{
    /// <summary>byPrice sıralamayı değiştirir; drop bir seçimi ("marka"/"oz" + değer, "stok", "kampanya", "fiyat") çıkarır;
    /// onlySort yalnız terim ve sıralamayı bırakır (tümünü temizle).</summary>
    public string Url(bool? byPrice = null, (string Key, string? Value)? drop = null, bool onlySort = false)
    {
        var parts = new List<(string Key, string? Value)> { ("q", Term), ("sirala", (byPrice ?? ByPrice) ? "fiyat" : null) };
        if (!onlySort)
        {
            if (drop?.Key != "fiyat")
            {
                parts.Add(("min", StoreCatalog.Amount(Min)));
                parts.Add(("max", StoreCatalog.Amount(Max)));
            }

            parts.AddRange(Selection.BrandSlugs.Where(s => drop != ("marka", s)).Select(s => ("marka", (string?)s)));
            parts.AddRange(Selection.AttributePairs
                .Select(p => p.Name + ":" + p.Value)
                .Where(v => drop != ("oz", v))
                .Select(v => ("oz", (string?)v)));
            if (Selection.InStockOnly && drop?.Key != "stok")
            {
                parts.Add(("stok", "1"));
            }

            if (Selection.CampaignOnly && drop?.Key != "kampanya")
            {
                parts.Add(("kampanya", "1"));
            }
        }

        return StoreCatalog.Url(Path, parts.ToArray());
    }
}

/// <summary>Liste bölümü (_Listing); EmptyLink süzgeçsiz boş rafta önerilen başka yer.</summary>
public sealed record ListingVm(
    SortTabsVm Sort,
    FilterPanelVm Filter,
    IReadOnlyList<ProductCardVm> Cards,
    PaginationVm Pagination,
    CategoryTabVm? EmptyLink = null);

/// <summary>Count null: sayısı gösterilmeyen seçenek (stokta olanlar, kampanyalı).</summary>
public sealed record FilterOptionVm(string Name, string Value, string Label, int? Count, bool Checked);

public sealed record FilterGroupVm(string Title, IReadOnlyList<FilterOptionVm> Options);

public sealed record FilterChipVm(string Label, string RemoveUrl);

/// <summary>Süzgeç paneli: GET formu (terim ve sıralama gizli alanda), fiyat aralığı, seçenek grupları; seçili süzgeçler
/// listenin üstünde çip olarak kaldırılır.</summary>
public sealed record FilterPanelVm(
    string Action,
    IReadOnlyList<HiddenFieldVm> Hidden,
    decimal? Min,
    decimal? Max,
    IReadOnlyList<FilterGroupVm> Groups,
    IReadOnlyList<FilterChipVm> Chips,
    string ClearUrl,
    int Total)
{
    public int ActiveCount => Chips.Count;
}
