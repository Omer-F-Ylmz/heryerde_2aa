using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfSlugHistoryDal : EfEntityRepositoryBase<SlugHistory, HerYerdeContext>, ISlugHistoryDal
{
    public EfSlugHistoryDal(HerYerdeContext context) : base(context)
    {
    }

    public async Task RecordAsync(string entityType, int entityId, string oldSlug, string newSlug, DateTime at, CancellationToken cancellationToken = default)
    {
        var existing = await Context.SlugHistories
            .FirstOrDefaultAsync(h => h.EntityType == entityType && h.OldSlug == oldSlug, cancellationToken);
        if (existing is null)
        {
            Context.SlugHistories.Add(new SlugHistory { EntityType = entityType, EntityId = entityId, OldSlug = oldSlug, CreatedAt = at });
        }
        else
        {
            existing.EntityId = entityId;
            existing.CreatedAt = at;
        }

        Context.SlugHistories.RemoveRange(await Context.SlugHistories
            .Where(h => h.EntityType == entityType && h.OldSlug == newSlug)
            .ToListAsync(cancellationToken));
    }

    public Task<int?> FindEntityIdAsync(string entityType, string oldSlug, CancellationToken cancellationToken = default)
        => Context.SlugHistories
            .Where(h => h.EntityType == entityType && h.OldSlug == oldSlug)
            .Select(h => (int?)h.EntityId)
            .FirstOrDefaultAsync(cancellationToken);
}
