using System.Text.Json;
using HerYerde.Entities.Concrete;

namespace HerYerde.Web.Models;

/// <summary>Vitrin seçim kuralları; DB'siz, saf fonksiyonlar.</summary>
public static class StoreCatalog
{
    public const int PageSize = 24;

    public static bool IsCampaignActive(Product product, DateTime now)
        => product.CampaignPrice is { } campaign
           && campaign < product.Price
           && (product.CampaignEndsAt is null || product.CampaignEndsAt > now);

    /// <summary>Aktif kampanyalılardan en yakın biten; süresizler en sona.</summary>
    public static Product? PickHero(IEnumerable<Product> products, DateTime now)
        => products
            .Where(p => p.IsActive && IsCampaignActive(p, now))
            .OrderBy(p => p.CampaignEndsAt ?? DateTime.MaxValue)
            .ThenBy(p => p.Id)
            .FirstOrDefault();

    public static decimal EffectivePrice(Product product, DateTime now)
        => IsCampaignActive(product, now) ? product.CampaignPrice!.Value : product.Price;

    public static IEnumerable<Product> Sort(IEnumerable<Product> products, string? sort, DateTime now)
        => sort == "fiyat"
            ? products.OrderBy(p => EffectivePrice(p, now)).ThenBy(p => p.Id)
            : products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);

    public static List<Product> Similar(IEnumerable<Product> products, Product self, int take = 4)
        => products
            .Where(p => p.IsActive && p.Id != self.Id && p.CategoryId == self.CategoryId)
            .Take(take)
            .ToList();

    public static string WhatsAppUrl(string baseUrl, string productName)
        => baseUrl + "?text=" + Uri.EscapeDataString($"Merhaba, {productName} için sipariş vermek istiyorum.");

    /// <summary>Görselsiz üründe alt kategoriye göre tek çizgi ikon; bilinmeyen slug → ikon yok.</summary>
    public static string? PlaceholderIcon(string? categorySlug) => categorySlug switch
    {
        "tencere-tava" => "pot",
        "yemek-takimi" => "plate",
        "catal-kasik" => "cutlery",
        "saklama-duzenleme" => "box",
        "sepet-dekor" => "basket",
        "kucuk-ev-aletleri" => "fan",
        _ => null
    };

    public static ProductCardVm Card(Product product, IEnumerable<ProductImage> images, DateTime now, bool lazy = true, string? categorySlug = null)
    {
        var ordered = images.Where(i => i.ProductId == product.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ToList();
        var active = IsCampaignActive(product, now);
        return new ProductCardVm
        {
            Name = product.Name,
            Slug = product.Slug,
            Price = product.Price,
            CampaignPrice = active ? product.CampaignPrice : null,
            Badge = active ? new BadgeVm(BadgeKind.Campaign, product.CampaignLabel ?? "Kampanya") : null,
            ImageUrl = ordered.ElementAtOrDefault(0)?.Url,
            SecondImageUrl = ordered.ElementAtOrDefault(1)?.Url,
            ImageAlt = ordered.ElementAtOrDefault(0)?.Alt ?? product.Name,
            PlaceholderIcon = PlaceholderIcon(categorySlug),
            Lazy = lazy
        };
    }

    public static VariantPickerVm Picker(IEnumerable<ProductVariant> variants)
    {
        var list = variants.ToList();
        return new VariantPickerVm(
            Group(list, v => v.Size),
            Group(list, v => v.Color),
            list.Select(v => new VariantOptionVm(v.Id, v.Size, v.Color, v.Stock)).ToList());

        static List<VariantChipVm> Group(List<ProductVariant> list, Func<ProductVariant, string?> key)
            => list.Where(v => !string.IsNullOrWhiteSpace(key(v)))
                .GroupBy(v => key(v)!)
                .Select(g => new VariantChipVm(g.Key, g.Sum(v => v.Stock)))
                .ToList();
    }
}

/// <summary>docs/testimonials.json (çıktı dizinine kopyalanır) → DM alıntıları.</summary>
public static class TestimonialSource
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<TestimonialVm> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "testimonials.json");
        if (!File.Exists(path))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<TestimonialVm>>(File.ReadAllText(path), Options) ?? [];
    }
}

public sealed record HomeVm(
    Product? Hero,
    string? HeroImage,
    string? HeroPlaceholderIcon,
    string? HeroWhatsApp,
    IReadOnlyList<ProductCardVm> NewArrivals,
    IReadOnlyList<TestimonialVm> Testimonials);

public sealed record CategoryPageVm(
    string Title,
    IReadOnlyList<CategoryTabVm> Tabs,
    string? Sort,
    string SortBaseUrl,
    IReadOnlyList<ProductCardVm> Cards,
    PaginationVm Pagination);

public sealed record ProductPageVm(
    Product Product,
    string CategoryName,
    string? CategorySlug,
    bool IsClothing,
    IReadOnlyList<ProductImage> Images,
    VariantPickerVm Picker,
    bool CampaignActive,
    string WhatsAppUrl,
    IReadOnlyList<ProductCardVm> Similar)
{
    public string? PlaceholderIcon => StoreCatalog.PlaceholderIcon(CategorySlug);

    /// <summary>Beden/renk seçimini varyant kimliğine çeviren tablo; sepet formu bunu okur.</summary>
    public string VariantsJson => JsonSerializer.Serialize(Picker.Options ?? []);
}
