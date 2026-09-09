using System.Net;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IProductService
{
    Task<(HttpStatusCode, IDataResult<List<Product>>)> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Okuma amaçlı: dönen kayıt izlenmez, üzerinde yapılan değişiklik veritabanına yazılmaz.</summary>
    Task<(HttpStatusCode, IDataResult<Product>)> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<Product>)> AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Giyim ürünü varyantsızken yayına alınamaz.</summary>
    Task<(HttpStatusCode, IResult)> UpdateAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Ürün silinmez, soft delete ile listelerden düşer.</summary>
    Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<List<ProductVariant>>)> GetVariantsAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Ev ürününde beden/renk boş kalmak zorunda.</summary>
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
}
