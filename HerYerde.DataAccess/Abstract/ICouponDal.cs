using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

/// <summary>Kupon ve yönetim listesindeki kullanım sayısı.</summary>
public sealed record CouponRow(Coupon Coupon, int Used);

public interface ICouponDal : IEntityRepository<Coupon>
{
    /// <summary>Yönetim listesi: en yeniden eskiye, her kuponun iptal edilmemiş siparişlerdeki kullanım sayısıyla.</summary>
    Task<List<CouponRow>> GetForAdminAsync(int take, CancellationToken cancellationToken = default);
}
