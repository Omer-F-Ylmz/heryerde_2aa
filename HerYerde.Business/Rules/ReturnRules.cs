using System.Numerics;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Rules;

public static class ReturnRules
{
    /// <summary>Cayma süresi: teslimden itibaren 14 gün (Mesafeli Sözleşmeler Yönetmeliği m. 9).</summary>
    public static readonly TimeSpan RequestWindow = TimeSpan.FromDays(14);

    /// <summary>Geri ödeme süresi: cayma bildiriminin ulaşmasından itibaren 14 gün (m. 13).</summary>
    public const int RefundDays = 14;

    /// <summary>Teslim anı yoksa (alan eklenmeden önce teslim edilmiş sipariş) sipariş anından sayılır.</summary>
    public static bool CanRequest(Order order, DateTime now)
        => order.Status == OrderStatus.TeslimEdildi && now <= (order.DeliveredAt ?? order.CreatedAt) + RequestWindow;

    /// <summary>Müşteri siparişi paket hazırlanmaya başlamadan iptal edebilir.</summary>
    public static bool CanCustomerCancel(OrderStatus status) => status is OrderStatus.Beklemede or OrderStatus.Onaylandi;

    /// <summary>Geri ödemesi bekleyen iadede kalan gün (eksi: süre geçti); değişimde, retde ve tamamlanmışta null.</summary>
    public static int? RefundDaysLeft(ReturnRequest request, DateTime now)
        => request.Type == ReturnType.Iade && request.Status is ReturnStatus.Bekliyor or ReturnStatus.Onaylandi or ReturnStatus.TeslimAlindi
            ? RefundDays - (int)Math.Floor((now - request.CreatedAt).TotalDays)
            : null;
}

public static class IbanRules
{
    /// <summary>TR IBAN: "TR" + 24 hane, boşluklar atılır, mod-97 denetimi tutmalı; sonuç boşluksuz büyük harf.</summary>
    public static bool TryNormalize(string? iban, out string normalized)
    {
        normalized = string.Concat((iban ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        if (normalized.Length != 26 || !normalized.StartsWith("TR", StringComparison.Ordinal) || !normalized[2..].All(char.IsAsciiDigit))
        {
            return false;
        }

        // Ülke kodu ve kontrol haneleri sona alınır, harfler sayıya (T=29, R=27) çevrilir; kalan 1 olmalı.
        var digits = normalized[4..] + "2927" + normalized[2..4];
        return BigInteger.Parse(digits) % 97 == 1;
    }
}
