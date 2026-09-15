using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

/// <summary>KVKK başvuru aracı: kişiyi telefon (0/90/+90 aynı) ya da e-postayla bulur, dökümünü verir, tümünü anonimleştirir.</summary>
public interface IKvkkService
{
    /// <summary>Telefon ya da e-posta değilse 400. Siparişler telefon/e-postayla, iletişim mesajları iletişim alanıyla, yorumlar sipariş numarasıyla eşleşir.</summary>
    Task<(HttpStatusCode, IDataResult<KvkkPerson>)> FindAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Kapanmış siparişler anonimleştirilir (dekont ve iade fotoğrafı dahil), açıklar atlanır; mesajlar silinir, yorum adı maskelenir.</summary>
    Task<(HttpStatusCode, IDataResult<KvkkAnonymizeResult>)> AnonymizeAllAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Kişi telefon ya da e-posta değilse 400; tek biçime indirgenip saklanır.</summary>
    Task<(HttpStatusCode, IDataResult<KvkkRequest>)> OpenRequestAsync(string subject, DateTime receivedAt, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> CompleteRequestAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Başvurular, açıklar önce, en eskiden yeniye.</summary>
    Task<(HttpStatusCode, IDataResult<List<KvkkRequestRow>>)> GetRequestsAsync(CancellationToken cancellationToken = default);
}
