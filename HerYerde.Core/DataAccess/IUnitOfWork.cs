namespace HerYerde.Core.DataAccess;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Birden çok kaydetme adımı gereken iş için: hepsi ya birlikte yazılır ya hiçbiri.</summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default);
}
