using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IPaymentService
{
    /// <summary>Kartla açılmış siparişin ödemesini başlatır; başarısızsa sipariş iptal edilir, 502 döner.</summary>
    Task<(HttpStatusCode, IDataResult<ThreeDsForm>)> StartAsync(int orderId, PaymentCard card, string buyerIp, CancellationToken cancellationToken = default);

    /// <summary>3D dönüşünü işler. 200: ödendi; 402: reddedildi (sipariş iptal); 404: bilinmeyen kayıt.
    /// Aynı dönüş tekrar gelirse ilk sonuç döner, stok ikinci kez düşmez.</summary>
    Task<(HttpStatusCode, IDataResult<Order>)> CompleteAsync(PaymentCallback callback, CancellationToken cancellationToken = default);
}
