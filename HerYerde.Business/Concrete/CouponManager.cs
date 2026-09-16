using System.Globalization;
using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Concrete;

public class CouponManager : ICouponService
{
    /// <summary>Yönetim listesinde gösterilen en yeni kayıt sayısı.</summary>
    public const int Window = 200;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly ICouponDal _couponDal;
    private readonly ICartDal _cartDal;
    private readonly IOrderDal _orderDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public CouponManager(
        ICouponDal couponDal,
        ICartDal cartDal,
        IOrderDal orderDal,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _couponDal = couponDal;
        _cartDal = cartDal;
        _orderDal = orderDal;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IResult)> ApplyAsync(Guid cartId, string? code, decimal subtotal, CancellationToken cancellationToken = default)
    {
        var evaluated = await EvaluateAsync(code, subtotal, cancellationToken: cancellationToken);
        if (!evaluated.Valid)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(evaluated.Problem ?? "Kupon kodu geçersiz."));
        }

        var cart = await _cartDal.GetTrackedAsync(c => c.Id == cartId, cancellationToken);
        if (cart is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sepet bulunamadı."));
        }

        cart.CouponCode = evaluated.Code;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult($"{evaluated.Code} kuponu uygulandı."));
    }

    public async Task<(HttpStatusCode, IResult)> RemoveAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var cart = await _cartDal.GetTrackedAsync(c => c.Id == cartId, cancellationToken);
        if (cart is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sepet bulunamadı."));
        }

        if (cart.CouponCode is not null)
        {
            cart.CouponCode = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (HttpStatusCode.OK, new SuccessResult("Kupon kaldırıldı."));
    }

    public async Task<CouponView> EvaluateAsync(
        string? code,
        decimal subtotal,
        string? phone = null,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = CouponRules.Normalize(code);
        if (normalized.Length == 0)
        {
            return CouponView.None;
        }

        var coupon = await _couponDal.GetAsync(c => c.Code == normalized, cancellationToken);
        var now = _clock.GetUtcNow().UtcDateTime;
        if (coupon is null || !coupon.IsActive)
        {
            return Problem("Kupon kodu bulunamadı.");
        }

        if (coupon.StartsAt > now)
        {
            return Problem("Kupon henüz başlamadı.");
        }

        if (coupon.EndsAt <= now)
        {
            return Problem("Kuponun süresi doldu.");
        }

        if (subtotal < coupon.MinSubtotal)
        {
            return Problem($"Bu kupon en az {Tl(coupon.MinSubtotal)} tutarındaki sepetlerde geçerli.");
        }

        if (coupon.TotalLimit is { } totalLimit
            && await _orderDal.CouponUsageAsync(coupon.Code, null, null, cancellationToken) >= totalLimit)
        {
            return Problem("Bu kuponun kullanım hakkı doldu.");
        }

        if (phone is not null
            && coupon.PerPersonLimit is { } perPerson
            && await _orderDal.CouponUsageAsync(coupon.Code, phone, email, cancellationToken) >= perPerson)
        {
            return Problem("Bu kuponu daha önce kullandınız.");
        }

        return new CouponView(coupon.Code, CouponRules.Discount(coupon, subtotal), CouponRules.IsFreeShipping(coupon));

        CouponView Problem(string message) => new(null, 0m, false, message);
    }

    public async Task<(HttpStatusCode, IDataResult<List<CouponRow>>)> GetForAdminAsync(CancellationToken cancellationToken = default)
        => (HttpStatusCode.OK, new SuccessDataResult<List<CouponRow>>(await _couponDal.GetForAdminAsync(Window, cancellationToken)));

    public async Task<(HttpStatusCode, IDataResult<Coupon>)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _couponDal.GetAsync(c => c.Id == id, cancellationToken) is { } found
            ? (HttpStatusCode.OK, new SuccessDataResult<Coupon>(found))
            : (HttpStatusCode.NotFound, new ErrorDataResult<Coupon>("Kupon bulunamadı."));

    public async Task<(HttpStatusCode, IDataResult<Coupon>)> SaveAsync(Coupon coupon, CancellationToken cancellationToken = default)
    {
        var code = CouponRules.Normalize(coupon.Code);
        if (code.Length is 0 or > CouponRules.MaxCodeLength)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Coupon>($"Kod zorunlu (en çok {CouponRules.MaxCodeLength} karakter, boşluksuz)."));
        }

        if (coupon.EndsAt <= coupon.StartsAt)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Coupon>("Bitiş tarihi başlangıçtan sonra olmalı."));
        }

        if (coupon.Kind == CouponKind.Yuzde && coupon.Value is <= 0m or > 100m)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Coupon>("Yüzde kuponunda oran 1 ile 100 arasında olmalı."));
        }

        if (coupon.Kind == CouponKind.Tutar && coupon.Value <= 0m)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Coupon>("Tutar kuponunda indirim sıfırdan büyük olmalı."));
        }

        if (await _couponDal.GetAsync(c => c.Code == code && c.Id != coupon.Id, cancellationToken) is not null)
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<Coupon>("Bu kod başka bir kuponda kullanılıyor."));
        }

        var stored = coupon.Id > 0 ? await _couponDal.GetTrackedAsync(c => c.Id == coupon.Id, cancellationToken) : null;
        if (coupon.Id > 0 && stored is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<Coupon>("Kupon bulunamadı."));
        }

        var target = stored ?? new Coupon { CreatedAt = _clock.GetUtcNow().UtcDateTime };
        target.Code = code;
        target.Kind = coupon.Kind;
        // Kargo bedava kuponunda tutar alanı anlamsız: karışmasın diye sıfırlanır.
        target.Value = coupon.Kind == CouponKind.KargoBedava ? 0m : coupon.Value;
        target.MinSubtotal = Math.Max(0m, coupon.MinSubtotal);
        target.StartsAt = coupon.StartsAt;
        target.EndsAt = coupon.EndsAt;
        target.TotalLimit = coupon.TotalLimit is > 0 ? coupon.TotalLimit : null;
        target.PerPersonLimit = coupon.PerPersonLimit is > 0 ? coupon.PerPersonLimit : null;
        target.IsActive = coupon.IsActive;

        if (stored is null)
        {
            await _couponDal.AddAsync(target, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<Coupon>(target, "Kupon kaydedildi."));
    }

    public async Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var stored = await _couponDal.GetTrackedAsync(c => c.Id == id, cancellationToken);
        if (stored is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Kupon bulunamadı."));
        }

        // Siparişlerdeki coupon_code metin olarak durur: kupon silinse de geçmiş sipariş bozulmaz.
        _couponDal.Delete(stored);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Kupon silindi."));
    }

    private static string Tl(decimal amount) => amount.ToString("N2", Turkish) + " ₺";
}
