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

/// <summary>İade/değişim talebinin müşteriye ve yöneticiye görünen adı.</summary>
public static class ReturnLabels
{
    public static string Type(ReturnType type) => type == ReturnType.Degisim ? "Değişim" : "İade";

    public static string Status(ReturnStatus status) => status switch
    {
        ReturnStatus.Onaylandi => "Onaylandı, ürün bekleniyor",
        ReturnStatus.Reddedildi => "Reddedildi",
        ReturnStatus.TeslimAlindi => "Ürün teslim alındı, geri ödeme yapılacak",
        ReturnStatus.Tamamlandi => "Tamamlandı",
        _ => "İnceleniyor"
    };
}
