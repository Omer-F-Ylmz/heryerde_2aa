using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfPaymentDal : EfEntityRepositoryBase<Payment, HerYerdeContext>, IPaymentDal
{
    public EfPaymentDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<int> TryCloseAsync(int paymentId, PaymentStatus status, DateTime moment, CancellationToken cancellationToken = default)
        => Context.Payments
            .Where(p => p.Id == paymentId && p.Status == PaymentStatus.Baslatildi)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, status)
                .SetProperty(p => p.UpdatedAt, moment), cancellationToken);

    public Task<int> TryRefundAsync(int paymentId, DateTime moment, CancellationToken cancellationToken = default)
        => Context.Payments
            .Where(p => p.Id == paymentId && p.Status == PaymentStatus.Basarili)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Iade)
                .SetProperty(p => p.UpdatedAt, moment), cancellationToken);
}
