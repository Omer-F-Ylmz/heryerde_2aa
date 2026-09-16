using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class SearchLogManager : ISearchLogService
{
    public const int TermLength = 60;
    public const int ReportSize = 50;

    /// <summary>Bu kadar rakam içeren terim telefon numarası sayılır.</summary>
    private const int PhoneDigits = 7;

    private readonly ISearchLogDal _searchLogDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public SearchLogManager(ISearchLogDal searchLogDal, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _searchLogDal = searchLogDal;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task RecordAsync(string term, int resultCount, CancellationToken cancellationToken = default)
    {
        var normalized = SearchTerms.Normalize(term);
        // Kutuya telefon ya da e-posta yazılmış olabilir: günlük kişisel veri taşımasın diye böyle terim hiç yazılmaz.
        if (normalized.Length == 0 || normalized.Contains('@') || normalized.Count(char.IsDigit) >= PhoneDigits)
        {
            return;
        }

        await _searchLogDal.AddAsync(new SearchLog
        {
            Term = normalized.Length > TermLength ? normalized[..TermLength] : normalized,
            ResultCount = resultCount,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SearchReport> GetReportAsync(TimeSpan window, CancellationToken cancellationToken = default)
    {
        var since = _clock.GetUtcNow().UtcDateTime - window;
        var zero = await _searchLogDal.GetTermsAsync(since, zeroResultsOnly: true, ReportSize, cancellationToken);
        var top = await _searchLogDal.GetTermsAsync(since, zeroResultsOnly: false, ReportSize, cancellationToken);
        return new SearchReport(zero, top);
    }

    public Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default)
        => _searchLogDal.DeleteOlderThanAsync(_clock.GetUtcNow().UtcDateTime - age, cancellationToken);
}
