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

    /// <summary>Kartla ödenmiş siparişin tamamını iade eder: sağlayıcıda iade, Payment.Iade, sipariş iptal, stok geri — tek işlemde.
    /// İade edilecek ödeme yoksa ya da zaten iade edildiyse 409 (sağlayıcı çağrılmaz); sağlayıcı reddederse 502 ve hiçbir şey değişmez.</summary>
    Task<(HttpStatusCode, IResult)> RefundAsync(int orderId, string ip, CancellationToken cancellationToken = default);
}
