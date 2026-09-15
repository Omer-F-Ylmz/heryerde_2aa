using HerYerde.Entities.Enums;

namespace HerYerde.Business.Rules;

/// <summary>Ödeme yöntemi ve durumunun müşteriye/yöneticiye görünen adı; sayfa ve e-posta aynı dili konuşsun.</summary>
public static class PaymentLabels
{
    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.HavaleEft => "Havale / EFT",
        PaymentMethod.KrediKarti => "Kredi kartı",
        PaymentMethod.NakitElden => "Nakit (elden)",
        _ => "Kapıda ödeme"
    };

    public static string Source(OrderSource source) => source switch
    {
        OrderSource.WhatsApp => "WhatsApp",
        OrderSource.Instagram => "Instagram",
        OrderSource.Telefon => "Telefon",
        OrderSource.Magaza => "Mağaza",
        _ => "Site"
    };

    public static string Status(PaymentStatus status) => status switch
    {
        PaymentStatus.Basarili => "Ödeme alındı",
        PaymentStatus.Basarisiz => "Ödeme alınamadı",
        PaymentStatus.Iade => "İade edildi",
        _ => "Ödeme bekleniyor"
    };
}
