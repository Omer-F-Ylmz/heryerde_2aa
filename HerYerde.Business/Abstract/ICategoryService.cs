using System.Net;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface ICategoryService
{
    Task<(HttpStatusCode, IDataResult<List<Category>>)> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(HttpStatusCode, IDataResult<Category>)> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(HttpStatusCode, IResult)> AddAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Kök kategoride slug korunur; ad, sıra ve yayın durumu değişebilir.</summary>
    Task<(HttpStatusCode, IResult)> UpdateAsync(Category category, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Kategori görselini değiştirir; eski adres (dosyası silinsin diye) veriyle döner.</summary>
    Task<(HttpStatusCode, IDataResult<string?>)> SetImageAsync(int id, string? imageUrl, CancellationToken cancellationToken = default);

    /// <summary>Eski slug'ın bugünkü kategorisi (301 için); yoksa 404.</summary>
    Task<(HttpStatusCode, IDataResult<Category>)> GetByOldSlugAsync(string oldSlug, CancellationToken cancellationToken = default);
}
