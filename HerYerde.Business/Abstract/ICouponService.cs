using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface ICouponService
{
    /// <summary>Kodu sepete yazar. Kupon yoksa, kapalıysa, süresi dolmuşsa, ara toplam yetmiyorsa ya da toplam
    /// kullanım limiti dolmuşsa 400 döner ve sepet değişmez. Kişi başı limit ancak sipariş anında bilinir.</summary>
    Task<(HttpStatusCode, IResult)> ApplyAsync(Guid cartId, string? code, decimal subtotal, CancellationToken cancellationToken = default);

    /// <summary>Sepetteki kuponu kaldırır; kupon yoksa da başarılı sayılır.</summary>
    Task<(HttpStatusCode, IResult)> RemoveAsync(Guid cartId, CancellationToken cancellationToken = default);

    /// <summary>Kodun bu tutar ve kişi için sonucu. Telefon verilirse kişi başı limit de sınanır.
    /// Kod boşsa ya da geçersizse indirimsiz bir sonuç döner (Problem dolu).</summary>
    Task<CouponView> EvaluateAsync(
        string? code,
        decimal subtotal,
        string? phone = null,
        string? email = null,
        CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<List<CouponRow>>)> GetForAdminAsync(CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<Coupon>)> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Kod zorunlu ve tekil, bitiş başlangıçtan sonra, yüzde kuponunda oran 1-100 arası.</summary>
    Task<(HttpStatusCode, IDataResult<Coupon>)> SaveAsync(Coupon coupon, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
