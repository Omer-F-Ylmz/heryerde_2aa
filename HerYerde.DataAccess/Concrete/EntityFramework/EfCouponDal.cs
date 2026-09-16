using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfCouponDal : EfEntityRepositoryBase<Coupon, HerYerdeContext>, ICouponDal
{
    public EfCouponDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<List<CouponRow>> GetForAdminAsync(int take, CancellationToken cancellationToken = default)
        => Context.Coupons.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(take)
            .Select(c => new CouponRow(
                c,
                Context.Orders.Count(o => o.CouponCode == c.Code && o.Status != OrderStatus.IptalEdildi)))
            .ToListAsync(cancellationToken);
}
