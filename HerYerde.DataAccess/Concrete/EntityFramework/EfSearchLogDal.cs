using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfSearchLogDal : EfEntityRepositoryBase<SearchLog, HerYerdeContext>, ISearchLogDal
{
    public EfSearchLogDal(HerYerdeContext context) : base(context)
    {
    }

    public async Task<List<SearchTermRow>> GetTermsAsync(DateTime since, bool zeroResultsOnly, int take, CancellationToken cancellationToken = default)
    {
        var rows = await Context.SearchLogs.AsNoTracking()
            .Where(l => l.CreatedAt >= since && (!zeroResultsOnly || l.ResultCount == 0))
            .GroupBy(l => l.Term)
            .Select(g => new { Term = g.Key, Searches = g.Count(), MaxResults = g.Max(l => l.ResultCount), Last = g.Max(l => l.CreatedAt) })
            .OrderByDescending(r => r.Searches)
            .ThenBy(r => r.Term)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SearchTermRow(r.Term, r.Searches, r.MaxResults, r.Last)).ToList();
    }

    public Task<int> DeleteOlderThanAsync(DateTime limit, CancellationToken cancellationToken = default)
        => Context.SearchLogs.Where(l => l.CreatedAt < limit).ExecuteDeleteAsync(cancellationToken);
}
