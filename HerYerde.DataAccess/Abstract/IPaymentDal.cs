using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.DataAccess.Abstract;

public interface IPaymentDal : IEntityRepository<Payment>
{
    /// <summary>Kaydı yalnız hâlâ <see cref="PaymentStatus.Baslatildi"/> ise kapatır; tek koşullu UPDATE olduğu için
    /// eşzamanlı iki dönüşten yalnız biri 1 alır, diğeri satır kilidini bekleyip 0 alır.</summary>
    Task<int> TryCloseAsync(int paymentId, PaymentStatus status, DateTime moment, CancellationToken cancellationToken = default);

    /// <summary>Kaydı yalnız <see cref="PaymentStatus.Basarili"/> ise <see cref="PaymentStatus.Iade"/> yapar; eşzamanlı ikinci
    /// iade isteği satır kilidini bekleyip 0 alır.</summary>
    Task<int> TryRefundAsync(int paymentId, DateTime moment, CancellationToken cancellationToken = default);
}
