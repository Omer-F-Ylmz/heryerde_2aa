using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IAdminAuditService
{
    /// <summary>Kaydın zamanı sunucu saatinden yazılır.</summary>
    Task<(HttpStatusCode, IResult)> LogAsync(AdminAuditLog entry, CancellationToken cancellationToken = default);

    /// <summary>Son 200 kayıt, en yeniden eskiye, 50'lik sayfalarla; aralık dışı sayfa sınıra çekilir.</summary>
    Task<(HttpStatusCode, IDataResult<AdminAuditPage>)> GetRecentAsync(int page, CancellationToken cancellationToken = default);

    /// <summary>Verilen yaştan eski kayıtları siler (saklama süresi, bkz. docs/veri-envanteri.md); silinen sayısını döner.</summary>
    Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default);
}
