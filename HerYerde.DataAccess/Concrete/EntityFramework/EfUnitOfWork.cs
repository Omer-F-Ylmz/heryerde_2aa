using HerYerde.Core.DataAccess;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly HerYerdeContext _context;

    public EfUnitOfWork(HerYerdeContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
