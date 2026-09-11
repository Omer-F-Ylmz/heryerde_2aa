using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using HerYerde.Entities.Concrete;

namespace HerYerde.Web.Models;

/// <summary>Arama motoru ve paylaşım etiketleri: mutlak adres, ürün açıklaması, JSON-LD blokları.</summary>
public static class Seo
{
    public const string InstagramUrl = "https://instagram.com/heryerde_2aa";

    private const int DescriptionLength = 150;

    /// <summary>Türkçe harfler okunur kalır; &lt; &gt; &amp; gibi HTML'e duyarlı karakterler her durumda kaçışlanır,
    /// bu yüzden satır içi blok &lt;/script&gt; ile erken kapanamaz.</summary>
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA)
    };

    /// <summary>Kök göreli yol Shop:BaseUrl ile birleşir; zaten http(s) olan adres olduğu gibi kalır.</summary>
    public static string Absolute(string baseUrl, string pathOrUrl)
        => Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? pathOrUrl
            : baseUrl.TrimEnd('/') + "/" + pathOrUrl.TrimStart('/');

    /// <summary>Ad · fiyat · açıklamanın ilk 150 karakteri.</summary>
    public static string ProductDescription(Product product, decimal price)
    {
        var text = product.Description.Trim();
        if (text.Length > DescriptionLength)
        {
            text = text[..DescriptionLength].TrimEnd() + "…";
        }

        var head = $"{product.Name} · {Money.Tl(price)}";
        return text.Length == 0 ? head : $"{head} · {text}";
    }

    public static string OrganizationJsonLd(string baseUrl) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "Organization",
        ["name"] = "HerYerde",
        ["url"] = Absolute(baseUrl, "/"),
        ["sameAs"] = new[] { InstagramUrl }
    });

    public static string ProductJsonLd(string baseUrl, Product product, decimal price, bool soldOut, IEnumerable<string> images)
    {
        var url = Absolute(baseUrl, "/urun/" + product.Slug);
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = product.Name,
            ["description"] = product.Description,
            ["url"] = url
        };

        var absoluteImages = images.Select(image => Absolute(baseUrl, image)).ToList();
        if (absoluteImages.Count > 0)
        {
            data["image"] = absoluteImages;
        }

        data["offers"] = new Dictionary<string, object?>
        {
            ["@type"] = "Offer",
            ["price"] = price,
            ["priceCurrency"] = "TRY",
            ["availability"] = soldOut ? "https://schema.org/OutOfStock" : "https://schema.org/InStock",
            ["url"] = url
        };

        return Serialize(data);
    }

    /// <summary>Ana sayfa → alan → alt kategori → ürün; bağlantısı olmayan ara halka (giyim) atlanır.</summary>
    public static string ProductBreadcrumbJsonLd(string baseUrl, ProductPageVm page)
    {
        var crumbs = new List<(string Name, string Url)> { ("Ana sayfa", "/") };
        if (page.RootUrl != "/")
        {
            crumbs.Add((page.RootName, page.RootUrl));
        }

        if (page.CategoryName is { } categoryName && page.CategoryUrl is { } categoryUrl)
        {
            crumbs.Add((categoryName, categoryUrl));
        }

        crumbs.Add((page.Product.Name, "/urun/" + page.Product.Slug));

        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = crumbs.Select((crumb, index) => new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                ["name"] = crumb.Name,
                ["item"] = Absolute(baseUrl, crumb.Url)
            }).ToList()
        });
    }

    private static string Serialize(Dictionary<string, object?> data) => JsonSerializer.Serialize(data, Json);
}
