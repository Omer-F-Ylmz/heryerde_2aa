using System.Net;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IBrandService
{
    /// <summary>Ada göre sıralı.</summary>
    Task<List<Brand>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Brand?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Brand?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Id 0 ise ekler, değilse günceller; adres addan üretilir. Aynı ad başka markadaysa 409.</summary>
    Task<(HttpStatusCode, IDataResult<Brand>)> SaveAsync(Brand brand, CancellationToken cancellationToken = default);

    /// <summary>Ürünü olan marka silinmez (409): ürünler önce başka markaya ya da markasıza alınır.</summary>
    Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
