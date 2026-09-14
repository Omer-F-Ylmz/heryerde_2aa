using System.Net;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IContactService
{
    /// <summary>Mesajı kaydeder ve mağazaya e-postayı aynı işlemde kuyruğa yazar; zaman sunucu saatinden.</summary>
    Task<(HttpStatusCode, IResult)> SendAsync(ContactMessage message, CancellationToken cancellationToken = default);

    /// <summary>Son 200 mesaj, en yeniden eskiye.</summary>
    Task<(HttpStatusCode, IDataResult<List<ContactMessage>>)> GetRecentAsync(CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> MarkReadAsync(int id, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Saklama süresi dolan mesajları siler; silinen sayısını döner.</summary>
    Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default);
}
