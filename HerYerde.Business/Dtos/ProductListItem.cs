using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Dtos;

/// <summary>Vitrin listeleri için ürün + kendi kategorisinin slug'ı; kategori tablosuna ikinci sorgu
/// gerekmez. SoldOut: Ev'de ürünün, Giyim'de tüm varyantların stoğu bitti.</summary>
public sealed record ProductListItem(Product Product, string CategorySlug, bool SoldOut, string? PreviewUrl = null);

/// <summary>Vitrin ürün sayfası: ürün ve varyantları (varyantsız üründe boş liste).</summary>
public sealed record ProductDetail(Product Product, IReadOnlyList<ProductVariant> Variants);

/// <summary>Sayfalanmış vitrin listesi: sayfa satırları ve süzgece uyan toplam ürün sayısı.</summary>
public sealed record ProductPage(List<ProductListItem> Items, int Total);
