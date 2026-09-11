using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class AdminAuditManager : IAdminAuditService
{
    public const int Window = 200;
    public const int PageSize = 50;

    private readonly IAdminAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public AdminAuditManager(IAdminAuditLogDal auditLogDal, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IResult)> LogAsync(AdminAuditLog entry, CancellationToken cancellationToken = default)
    {
        entry.At = _clock.GetUtcNow().UtcDateTime;
        await _auditLogDal.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Denetim kaydı yazıldı."));
    }

    public async Task<(HttpStatusCode, IDataResult<AdminAuditPage>)> GetRecentAsync(int page, CancellationToken cancellationToken = default)
    {
        var total = Math.Min(await _auditLogDal.CountAsync(cancellationToken), Window);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var current = Math.Clamp(page, 1, totalPages);
        var skip = (current - 1) * PageSize;
        var items = await _auditLogDal.GetRecentAsync(skip, Math.Min(PageSize, Window - skip), cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<AdminAuditPage>(new AdminAuditPage(items, current, totalPages)));
    }

    public async Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default)
    {
        var limit = _clock.GetUtcNow().UtcDateTime - age;
        var stale = await _auditLogDal.GetListAsync(a => a.At < limit, cancellationToken);
        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var entry in stale)
        {
            _auditLogDal.Delete(entry);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }
}
