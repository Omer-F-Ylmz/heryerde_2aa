using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

/// <summary>Vitrin satırı: ürün, kendi kategorisinin slug'ı ve stok bitti mi.</summary>
/// <summary>PreviewUrl: ürünün ilk videosunun kart önizlemesi; aynı sorguda alt sorguyla gelir.</summary>
public readonly record struct ProductRow(Product Product, string CategorySlug, bool SoldOut, string? PreviewUrl = null);

/// <summary>Sipariş satırının stok kodundan yayındaki ürüne köprü: kod varyant SKU'su ya da ürünün slug'ı olabilir.</summary>
public readonly record struct ProductSkuRow(string Sku, string Slug, string Name);

public interface IProductDal : IEntityRepository<Product>
{
    /// <summary>Yayındaki ürünler, kendi kategorilerinin slug'ıyla; süzme/sıralama/sayfalama ve
    /// süzgece uyan toplam sayı tek sorguda.</summary>
    Task<(List<ProductRow> Items, int Total)> GetActiveAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Sipariş satırlarının stok kodlarını yayındaki ürünün slug'ı ve adıyla eşler; yayından kalkan ürün gelmez.</summary>
    Task<List<ProductSkuRow>> ProductsBySkuAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken = default);

    /// <summary>Ana sayfa hero'su: kampanyası süren, en yakın biten ürün.</summary>
    Task<(Product Product, string CategorySlug)?> GetCampaignHeroAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Yönetim listesi: ürün kimliği başına varyant stok toplamı, tek gruplu sorguda.</summary>
    Task<Dictionary<int, int>> GetStockTotalsAsync(CancellationToken cancellationToken = default);

    /// <summary>Stoğu eşiğe (dahil) inmiş varyantsız ürünler ve varyantlar; stok takipsiz (NULL) ürün girmez.</summary>
    Task<List<LowStockRow>> GetLowStockAsync(int threshold, CancellationToken cancellationToken = default);

    /// <summary>Yayındaki ürün varyantlarıyla birlikte tek gidiş-dönüşte; yoksa null.</summary>
    Task<(Product Product, List<ProductVariant> Variants)?> GetActiveWithVariantsBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>GetLowStockAsync satır sayısı, tek sorguda (yönetim menüsündeki sayaç her sayfada çalışır).</summary>
    Task<int> CountLowStockAsync(int threshold, CancellationToken cancellationToken = default);

    /// <summary>Slug benzersizliği soft-delete süzgecini yok sayar: silinen ürünün slug'ı da doludur.</summary>
    Task<bool> SlugTakenAsync(string slug, int excludedId, CancellationToken cancellationToken = default);

    /// <summary>Stok takipli (stok NULL değil) üründen yeterliyse tek UPDATE ile düşer; etkilenen
    /// satır sayısını döner (0 = stok yetmedi ya da ürün stok tutmuyor).</summary>
    Task<int> TryDecrementStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Stok takipli ürüne slug'ıyla tek UPDATE ile iade eder; etkilenen satır sayısını döner
    /// (0 = o slug'da stok tutan ürün yok).</summary>
    Task<int> IncrementStockBySlugAsync(string slug, int quantity, CancellationToken cancellationToken = default);
}
