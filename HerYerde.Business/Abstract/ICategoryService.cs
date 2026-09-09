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
}
