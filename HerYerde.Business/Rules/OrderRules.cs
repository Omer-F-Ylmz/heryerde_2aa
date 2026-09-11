using HerYerde.Entities.Enums;

namespace HerYerde.Business.Rules;

public static class OrderRules
{
    /// <summary>Durum tek yönlü ilerler: aşama atlanmaz, geri dönülmez; iptal yalnız daha hiçbir işlem
    /// başlamamışken (Beklemede) yapılır.</summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to) => (from, to) switch
    {
        (OrderStatus.Beklemede, OrderStatus.Onaylandi) => true,
        (OrderStatus.Onaylandi, OrderStatus.Kargoda) => true,
        (OrderStatus.Kargoda, OrderStatus.TeslimEdildi) => true,
        (OrderStatus.Beklemede, OrderStatus.IptalEdildi) => true,
        _ => false
    };

    /// <summary>Kişisel veri yalnız sipariş kapandıktan sonra (teslim ya da iptal) anonimleştirilir; açık siparişte
    /// teyit ve teslimat için gerekir.</summary>
    public static bool CanAnonymize(OrderStatus status) => status is OrderStatus.TeslimEdildi or OrderStatus.IptalEdildi;
}

/// <summary>Sipariş numarası: "HY-yyyyMMdd-####". Sıra veritabanı SEQUENCE'ından gelir; geneldir,
/// gün başında sıfırlanmaz ve dört haneyi aşarsa uzar.</summary>
public static class OrderNo
{
    public static string PrefixFor(DateTime moment) => $"HY-{moment:yyyyMMdd}-";

    public static string Build(DateTime moment, long sequence) => PrefixFor(moment) + sequence.ToString("0000");
}

public static class PhoneRules
{
    /// <summary>TR cep telefonu: 5 ile başlayan 10 hane. "+90"/"0" önekleri ve boşluklar temizlenir,
    /// sonuç her zaman "05XXXXXXXXX" olur.</summary>
    public static bool TryNormalize(string? phone, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (phone.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '(' or ')' or '-' or '+')))
        {
            return false;
        }

        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12)
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith('0') && digits.Length == 11)
        {
            digits = digits[1..];
        }

        if (digits.Length != 10 || !digits.StartsWith('5'))
        {
            return false;
        }

        normalized = "0" + digits;
        return true;
    }
}
