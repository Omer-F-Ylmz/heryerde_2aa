using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

/// <summary>Arama raporu satırı: terim, aranma sayısı, en çok sonuç, son aranma.</summary>
public sealed record SearchTermRow(string Term, int Searches, int MaxResults, DateTime LastSearchedAt);

public interface ISearchLogDal : IEntityRepository<SearchLog>
{
    /// <summary><paramref name="since"/>'ten beri terim başına gruplu, en çok arananlar önde; zeroResultsOnly yalnız sonuçsuz aramaları sayar.</summary>
    Task<List<SearchTermRow>> GetTermsAsync(DateTime since, bool zeroResultsOnly, int take, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime limit, CancellationToken cancellationToken = default);
}
