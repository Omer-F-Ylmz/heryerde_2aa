using System.ComponentModel.DataAnnotations;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Entities.Enums;

namespace HerYerde.Web.Models;

public sealed class CheckoutFormViewModel
{
    [Required(ErrorMessage = "Ad soyad gerekli.")]
    [StringLength(120)]
    [Display(Name = "Ad soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon gerekli.")]
    [StringLength(20)]
    [Display(Name = "Cep telefonu")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta yazın.")]
    [StringLength(200)]
    [Display(Name = "E-posta (isteğe bağlı)")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Adres gerekli.")]
    [StringLength(500)]
    [Display(Name = "Adres")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "İl gerekli.")]
    [StringLength(60)]
    [Display(Name = "İl")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "İlçe gerekli.")]
    [StringLength(60)]
    [Display(Name = "İlçe")]
    public string District { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Sipariş notu (isteğe bağlı)")]
    public string? Note { get; set; }

    [Display(Name = "Ödeme yöntemi")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.KapidaOdeme;

    // Kart alanları yalnız sağlayıcıya iletilir; sayfaya geri yazılmaz, loglanmaz, saklanmaz.
    public string? CardHolderName { get; set; }

    public string? CardNumber { get; set; }

    public string? CardExpireMonth { get; set; }

    public string? CardExpireYear { get; set; }

    public string? CardCvc { get; set; }

    /// <summary>Ön bilgilendirme formu ve mesafeli satış sözleşmesi onayı; işaretsiz form sunucuda da reddedilir.</summary>
    [Range(typeof(bool), "true", "true",
        ErrorMessage = "Devam etmek için ön bilgilendirme formunu ve mesafeli satış sözleşmesini onaylayın.")]
    public bool LegalConsent { get; set; }
}

/// <summary>Notice: hata değil, kullanıcıya söylenmesi gereken değişiklik (ör. fiyat güncellendi).</summary>
public sealed record CheckoutPageViewModel(
    CheckoutFormViewModel Form,
    CartView Cart,
    string Iban,
    string? ErrorMessage,
    string? Notice = null,
    bool CardEnabled = false);

/// <summary>Bankanın 3D doğrulama sayfasına otomatik gönderilen form.</summary>
public sealed record ThreeDsViewModel(ThreeDsForm Form);

public sealed record ThankYouViewModel(OrderDetail Detail, string WhatsAppUrl, string Iban, string? TrackingUrl = null);

public sealed record CartPageViewModel(CartView Cart, string? ErrorMessage);

/// <summary>FRONT kiti: sepet satırı; düzenlenebilir halinde adet ve silme formlarını da çizer.</summary>
public sealed record CartLineViewModel(CartLine Line, bool Editable);

/// <summary>FRONT kiti: ara toplam / kargo / toplam üçlüsü; eşik verilirse bedava kargo çubuğu da çizilir.</summary>
public sealed record OrderSummaryViewModel(
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total,
    string? Note = null,
    decimal FreeShippingOver = 0m)
{
    public bool FreeShipping => ShippingRules.IsFree(Subtotal, FreeShippingOver);

    public decimal ToFreeShipping => ShippingRules.Remaining(Subtotal, FreeShippingOver);

    /// <summary>Eşik kapalıysa çubuk çizilmez.</summary>
    public bool ShowProgress => FreeShippingOver > 0m && Subtotal > 0m;

    /// <summary>Çubuğun dolu yüzdesi (0-100).</summary>
    public int Progress => FreeShippingOver <= 0m
        ? 0
        : (int)Math.Clamp(Math.Round(Subtotal / FreeShippingOver * 100m), 0m, 100m);

    /// <summary>Çubuğun genişliği sınıfla verilir: CSP "style-src 'self'" satır içi stili engelliyor,
    /// bu yüzden yüzde ona yuvarlanıp hazır sınıfa çevrilir.</summary>
    public int ProgressStep => (int)Math.Round(Progress / 10d) * 10;
}

/// <summary>Sipariş durumunun Türkçe karşılığı; rozet ve yönetim süzgeci aynı metni kullanır.</summary>
public static class OrderLabels
{
    public static string For(OrderStatus status) => status switch
    {
        OrderStatus.Beklemede => "Beklemede",
        OrderStatus.Onaylandi => "Onaylandı",
        OrderStatus.Kargoda => "Kargoda",
        OrderStatus.TeslimEdildi => "Teslim edildi",
        _ => "İptal edildi"
    };
}
