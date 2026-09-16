using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Rules;

/// <summary>Kupon kodu ve indirim hesabı tek yerden: sepet, ödeme adımı ve sipariş aynı sonucu verir. DB'siz.</summary>
public static class CouponRules
{
    public const int MaxCodeLength = 20;

    /// <summary>Kod büyük harfe çevrilir ve boşlukları atılır; "hosgeldin " ile "HOSGELDIN" aynı kupondur.
    /// Türkçe i/ı ayrımına takılmasın diye çevrim kültürden bağımsız.</summary>
    public static string Normalize(string? code)
        => new string((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    /// <summary>Kuponun para cinsinden indirimi; kargo bedava kuponunda indirim 0'dır (kargo ayrıca sıfırlanır).</summary>
    public static decimal Discount(Coupon coupon, decimal subtotal) => coupon.Kind switch
    {
        CouponKind.Yuzde => Math.Round(subtotal * coupon.Value / 100m, 2, MidpointRounding.AwayFromZero),
        CouponKind.Tutar => Math.Min(coupon.Value, subtotal),
        _ => 0m
    };

    public static bool IsFreeShipping(Coupon coupon) => coupon.Kind == CouponKind.KargoBedava;

    /// <summary>Kuponun tarih aralığında ve yayında olup olmadığı.</summary>
    public static bool IsLive(Coupon coupon, DateTime now)
        => coupon.IsActive && coupon.StartsAt <= now && coupon.EndsAt > now;
}
