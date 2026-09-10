using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Dtos;

/// <summary>Vitrin listeleri için ürün + kendi kategorisinin slug'ı; kategori tablosuna ikinci sorgu
/// gerekmez. SoldOut: Ev'de ürünün, Giyim'de tüm varyantların stoğu bitti.</summary>
public sealed record ProductListItem(Product Product, string CategorySlug, bool SoldOut);

/// <summary>Sayfalanmış vitrin listesi: sayfa satırları ve süzgece uyan toplam ürün sayısı.</summary>
public sealed record ProductPage(List<ProductListItem> Items, int Total);
