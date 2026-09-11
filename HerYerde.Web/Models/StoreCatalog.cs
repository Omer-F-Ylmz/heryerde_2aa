using System.Text.Json;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;

namespace HerYerde.Web.Models;

/// <summary>Vitrin seçim kuralları; DB'siz, saf fonksiyonlar.</summary>
public static class StoreCatalog
{
    public const int PageSize = 24;

    /// <summary>Arama terimi: baştan sondan boşluk atılır, 60 karakterde kesilir.</summary>
    public const int MinTermLength = 2;
    public const int MaxTermLength = 60;

    public static string Term(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        return trimmed.Length > MaxTermLength ? trimmed[..MaxTermLength] : trimmed;
    }

    /// <summary>Geçersiz aralık sessizce takas edilir: 300-100 yazan 100-300 görür.</summary>
    public static (decimal? Min, decimal? Max) Range(decimal? min, decimal? max)
        => min is { } low && max is { } high && low > high ? (high, low) : (min, max);

    /// <summary>Süzgeç ve sıralama, sayfalama/sıralama bağlantılarında korunur; boş değer yazılmaz.</summary>
    public static string Url(string path, params (string Key, string? Value)[] parts)
    {
        var query = string.Join("&", parts
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value!)));

        return query.Length == 0 ? path : path + "?" + query;
    }

    /// <summary>Fiyat alanları noktalı yazılır; tr-TR kültüründe virgüle dönmesin diye Invariant.</summary>
    public static string? Amount(decimal? value)
        => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsCampaignActive(Product product, DateTime now)
        => product.CampaignPrice is { } campaign
           && campaign < product.Price
           && (product.CampaignEndsAt is null || product.CampaignEndsAt > now);

    public static decimal EffectivePrice(Product product, DateTime now)
        => IsCampaignActive(product, now) ? product.CampaignPrice!.Value : product.Price;

    public static string WhatsAppUrl(string baseUrl, string productName)
        => baseUrl + "?text=" + Uri.EscapeDataString($"Merhaba, {productName} için sipariş vermek istiyorum.");

    /// <summary>Kırıntının kökü: vitrindeki ad ve bağlantı. Giyim alanının kendi rotası henüz yok,
    /// kök bağlantısı ana sayfaya gider.</summary>
    public static (string Name, string Url) Root(string? rootSlug, string rootName) => rootSlug switch
    {
        "ev" => ("Ev", "/ev"),
        "giyim" => ("Örtü & Eşarp", "/"),
        _ => (rootName, "/")
    };

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

    /// <summary>Rozet önceliği: tükendi > hediye > kampanya. Metin yöneticinin yazdığı etiketten gelir.</summary>
    public static BadgeVm? Badge(Product product, DateTime now, bool soldOut)
    {
        if (soldOut)
        {
            return new BadgeVm(BadgeKind.SoldOut, "Tükendi");
        }

        if (GiftRules.IsActive(product, now))
        {
            return new BadgeVm(BadgeKind.Gift, product.CampaignLabel ?? "1 alana 1 hediye");
        }

        return IsCampaignActive(product, now)
            ? new BadgeVm(BadgeKind.Campaign, product.CampaignLabel ?? "Kampanya")
            : null;
    }

    public static ProductCardVm Card(
        Product product,
        IEnumerable<ProductImage> images,
        DateTime now,
        bool lazy = true,
        string? categorySlug = null,
        bool soldOut = false)
    {
        var ordered = images.Where(i => i.ProductId == product.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ToList();
        var active = IsCampaignActive(product, now);
        return new ProductCardVm
        {
            Name = product.Name,
            Slug = product.Slug,
            Price = product.Price,
            CampaignPrice = active ? product.CampaignPrice : null,
            Badge = Badge(product, now, soldOut),
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
    SortTabsVm Sort,
    PriceFilterVm Filter,
    IReadOnlyList<ProductCardVm> Cards,
    PaginationVm Pagination,
    int Total);

/// <summary>Arama sonucu. Message doluysa (kısa terim) liste hiç sorgulanmaz.</summary>
public sealed record SearchPageVm(
    string Query,
    string? Message,
    SortTabsVm Sort,
    PriceFilterVm Filter,
    IReadOnlyList<ProductCardVm> Cards,
    IReadOnlyList<ProductCardVm> Suggestions,
    int Total,
    PaginationVm Pagination);

public sealed record ProductPageVm(
    Product Product,
    string RootName,
    string RootUrl,
    string? CategoryName,
    string? CategorySlug,
    bool IsClothing,
    bool SoldOut,
    IReadOnlyList<ProductImage> Images,
    VariantPickerVm Picker,
    bool CampaignActive,
    string WhatsAppUrl,
    IReadOnlyList<ProductCardVm> Similar)
{
    public string? PlaceholderIcon => StoreCatalog.PlaceholderIcon(CategorySlug);

    /// <summary>Alt kategori bağlantısı yalnız gezilebilir kökte (Ev) vardır; giyimin rotası henüz yok.</summary>
    public string? CategoryUrl => CategorySlug is { } slug && RootUrl == "/ev" ? "/ev/" + slug : null;

    /// <summary>Beden/renk seçimini varyant kimliğine çeviren tablo; sepet formu bunu okur.</summary>
    public string VariantsJson => JsonSerializer.Serialize(Picker.Options ?? []);
}
