using System.Linq.Expressions;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Core.DataAccess.EntityFramework;

public class EfEntityRepositoryBase<TEntity, TContext> : IEntityRepository<TEntity>
    where TEntity : class, IEntity, new()
    where TContext : DbContext
{
    protected EfEntityRepositoryBase(TContext context)
    {
        Context = context;
    }

    protected TContext Context { get; }

    public Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
        => Context.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(filter, cancellationToken);

    public Task<TEntity?> GetTrackedAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
        => Context.Set<TEntity>().FirstOrDefaultAsync(filter, cancellationToken);

    public Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = Context.Set<TEntity>().AsNoTracking();
        if (filter is not null)
        {
            query = query.Where(filter);
        }

        return query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await Context.Set<TEntity>().AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => Context.Set<TEntity>().Update(entity);

    public void Delete(TEntity entity) => Context.Set<TEntity>().Remove(entity);
}
