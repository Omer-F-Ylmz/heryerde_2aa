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
}
