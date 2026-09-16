using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Abstract;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IProductService
{
    Task<(HttpStatusCode, IDataResult<List<Product>>)> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Okuma amaçlı: dönen kayıt izlenmez, üzerinde yapılan değişiklik veritabanına yazılmaz.</summary>
    Task<(HttpStatusCode, IDataResult<Product>)> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Vitrin: yayındaki ürünler alt kategori slug'ıyla; süzme/sıralama/sayfalama SQL'de.</summary>
    Task<(HttpStatusCode, IDataResult<ProductPage>)> GetActiveAsync(ProductQuery query, CancellationToken cancellationToken = default);

    /// <summary>Süzgeç paneli seçenekleri; sayılar ziyaretçinin seçimlerinden bağımsız, kapsamdan.</summary>
    Task<ListingFacets> GetFacetsAsync(ProductQuery query, CancellationToken cancellationToken = default);

    /// <summary>Ana sayfa hero'su: kampanyası süren, en yakın biten ürün; yoksa null.</summary>
    Task<(HttpStatusCode, IDataResult<ProductListItem?>)> GetCampaignHeroAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Vitrin ürün sayfası: yayındaki ürünü varyantlarıyla birlikte slug'ıyla tek sorguda getirir.</summary>
    Task<(HttpStatusCode, IDataResult<ProductDetail>)> GetActiveBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<Product>)> AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Giyim ürünü varyantsızken yayına alınamaz.</summary>
    Task<(HttpStatusCode, IResult)> UpdateAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Ürün silinmez, soft delete ile listelerden düşer.</summary>
    Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<List<ProductVariant>>)> GetVariantsAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Birden çok ürünün varyantları tek sorguda (ürün akışı).</summary>
    Task<(HttpStatusCode, IDataResult<List<ProductVariant>>)> GetVariantsForAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default);

    /// <summary>Yönetim listesi: ürün kimliği başına varyant stok toplamı, tek sorguda.</summary>
    Task<(HttpStatusCode, IDataResult<Dictionary<int, int>>)> GetStockTotalsAsync(CancellationToken cancellationToken = default);

    /// <summary>Yönetim düşük stok listesi: stoğu eşiğe (dahil) inmiş ürün ve varyantlar.</summary>
    Task<(HttpStatusCode, IDataResult<List<LowStockRow>>)> GetLowStockAsync(int threshold, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<int>)> CountLowStockAsync(int threshold, CancellationToken cancellationToken = default);

    /// <summary>Eski slug'ın bugün yayındaki ürününün slug'ı (301 için); yoksa 404.</summary>
    Task<(HttpStatusCode, IDataResult<string>)> GetCurrentSlugAsync(string oldSlug, CancellationToken cancellationToken = default);

    /// <summary>Varyant her iki alanda (Giyim, Ev) serbesttir; ürün stoğu doluyken varyant eklenmez.</summary>
    Task<(HttpStatusCode, IResult)> AddVariantAsync(ProductVariant variant, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> UpdateStockAsync(int variantId, int stock, CancellationToken cancellationToken = default);
    Task<(HttpStatusCode, IResult)> DeleteVariantAsync(int variantId, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<List<ProductImage>>)> GetImagesAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Vitrin kartları için: verilen ürünlerin tüm görselleri tek sorguda.</summary>
    Task<(HttpStatusCode, IDataResult<List<ProductImage>>)> GetImagesForAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default);
    Task<(HttpStatusCode, IResult)> AddImageAsync(ProductImage image, CancellationToken cancellationToken = default);
    Task<(HttpStatusCode, IResult)> UpdateImageSortAsync(int imageId, int sortOrder, CancellationToken cancellationToken = default);

    /// <summary>Ürünün tek birincil görseli olur; işaretlenen dışındakiler düşer.</summary>
    Task<(HttpStatusCode, IResult)> SetPrimaryImageAsync(int imageId, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> DeleteImageAsync(int imageId, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<List<ProductVideo>>)> GetVideosAsync(int productId, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> AddVideoAsync(ProductVideo video, CancellationToken cancellationToken = default);
    Task<(HttpStatusCode, IResult)> DeleteVideoAsync(int videoId, CancellationToken cancellationToken = default);
}
