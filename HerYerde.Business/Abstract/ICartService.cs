using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface ICartService
{
    /// <summary>Çerezdeki sepet yoksa ya da düşmüşse yenisini açar.</summary>
    Task<(HttpStatusCode, IDataResult<Cart>)> GetOrCreateAsync(Guid? cartId, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<CartView>)> GetAsync(Guid cartId, CancellationToken cancellationToken = default);

    /// <summary>Aynı ürün-varyant zaten sepetteyse adet birleşir, fiyat ilk satırdan korunur.</summary>
    Task<(HttpStatusCode, IResult)> AddAsync(Guid cartId, int productId, int? variantId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Adet 0 verilirse satır silinir.</summary>
    Task<(HttpStatusCode, IResult)> SetQuantityAsync(Guid cartId, int itemId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>Başlıktaki rozet için toplam adet.</summary>
    Task<int> CountAsync(Guid? cartId, CancellationToken cancellationToken = default);

    /// <summary>Verilen süredir dokunulmamış anonim sepetleri satırlarıyla siler; silinen sepet sayısını döner.</summary>
    Task<int> PurgeStaleAsync(TimeSpan age, CancellationToken cancellationToken = default);
}
