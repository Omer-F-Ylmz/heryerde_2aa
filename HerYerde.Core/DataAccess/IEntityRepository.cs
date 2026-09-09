using System.Linq.Expressions;
using HerYerde.Core.Entities;

namespace HerYerde.Core.DataAccess;

public interface IEntityRepository<T> where T : class, IEntity, new()
{
    /// <summary>Okuma amaçlı: kayıt izlenmez.</summary>
    Task<T?> GetAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>Değiştirilip <see cref="IUnitOfWork.SaveChangesAsync"/> ile kaydedilecek kayıt için: izlenir.</summary>
    Task<T?> GetTrackedAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    Task<List<T>> GetListAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}
