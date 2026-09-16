namespace HerYerde.DataAccess.Abstract;

public enum ProductOrder
{
    /// <summary>En yeni önce.</summary>
    Newest,

    /// <summary>Kampanyalıda kampanya fiyatı esas alınarak artan.</summary>
    Price,

    /// <summary>Öne çıkanlar rafı: Product.FeaturedOrder artan, eşitlikte en yeni önce.</summary>
    Featured
}

/// <summary>Özellik süzgeci: ürünün bu addaki özelliği değerlerden biri olmalı.</summary>
public sealed record AttributeFilter(string Name, IReadOnlyCollection<string> Values);

/// <summary>Süzgeç panelinin seçenek satırı: marka (ad, slug) ya da özellik (ad, değer) ve ürün sayısı.</summary>
public sealed class FacetRow
{
    public const int BrandKind = 1;
    public const int AttributeKind = 2;
    public const int InStockKind = 3;
    public const int CampaignKind = 4;

    public int Kind { get; init; }
    public string First { get; init; } = string.Empty;
    public string Second { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>Vitrin listesi: süzme, sıralama ve sayfalama tek SQL sorgusuna çevrilir.</summary>
public sealed record ProductQuery
{
    /// <summary>Boşsa kategori süzgeci uygulanmaz.</summary>
    public IReadOnlyCollection<int> CategoryIds { get; init; } = [];

    public int? ExcludedProductId { get; init; }

    /// <summary>Boşsa marka kapsamı yok; doluysa ürün bu markalardan birinde (marka sayfası).</summary>
    public IReadOnlyCollection<int> BrandIds { get; init; } = [];

    /// <summary>Vitrin araması (D15): her iç liste bir kelimenin seçenekleri (kök + eş anlamlılar); seçeneklerden biri ad,
    /// açıklama ya da kategori adında geçmeli. Kelimeler VE ile birleşir.</summary>
    public IReadOnlyList<IReadOnlyList<string>> SearchWords { get; init; } = [];

    /// <summary>Ziyaretçinin seçtiği markalar (slug); boşsa süzgeç yok.</summary>
    public IReadOnlyCollection<string> BrandSlugs { get; init; } = [];

    /// <summary>Farklı adlar VE ile, aynı adın değerleri VEYA ile birleşir.</summary>
    public IReadOnlyList<AttributeFilter> Attributes { get; init; } = [];

    /// <summary>Tükenmişler dışarıda: stoğu 0 olan ürün ve bütün varyantları bitmiş ürün.</summary>
    public bool InStockOnly { get; init; }

    /// <summary>Yalnız süren kampanya fiyatı olanlar.</summary>
    public bool CampaignOnly { get; init; }

    /// <summary>Yalnız öne çıkan olarak işaretlenmiş ürünler.</summary>
    public bool FeaturedOnly { get; init; }

    /// <summary>Doluysa ad, açıklama ya da kategori adında geçen ürünler; LIKE ile SQL'de.</summary>
    public string? Term { get; init; }

    /// <summary>Etkin fiyat (kampanya sürüyorsa kampanya fiyatı) bu aralıkta kalır.</summary>
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }

    public ProductOrder Order { get; init; } = ProductOrder.Newest;

    /// <summary>Kampanyanın süresi dolmuş mu kararı bu ana göre verilir.</summary>
    public DateTime Now { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = int.MaxValue;
}
