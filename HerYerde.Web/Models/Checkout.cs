using System.ComponentModel.DataAnnotations;
using HerYerde.Business.Dtos;
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
}

/// <summary>Notice: hata değil, kullanıcıya söylenmesi gereken değişiklik (ör. fiyat güncellendi).</summary>
public sealed record CheckoutPageViewModel(
    CheckoutFormViewModel Form,
    CartView Cart,
    string Iban,
    string? ErrorMessage,
    string? Notice = null);

public sealed record ThankYouViewModel(OrderDetail Detail, string WhatsAppUrl, string Iban);

public sealed record CartPageViewModel(CartView Cart, string? ErrorMessage);

/// <summary>FRONT kiti: sepet satırı; düzenlenebilir halinde adet ve silme formlarını da çizer.</summary>
public sealed record CartLineViewModel(CartLine Line, bool Editable);

/// <summary>FRONT kiti: ara toplam / kargo / toplam üçlüsü.</summary>
public sealed record OrderSummaryViewModel(decimal Subtotal, decimal ShippingFee, decimal Total, string? Note = null);

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
