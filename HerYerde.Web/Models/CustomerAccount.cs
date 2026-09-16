using System.ComponentModel.DataAnnotations;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Web.Models;

public sealed class SignUpViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Boşsa parolasız hesap.</summary>
    [StringLength(200)]
    public string? Password { get; set; }

    public bool KvkkConsent { get; set; }

    public bool MarketingConsent { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Doluysa form yerine "bağlantı gönderdik" bildirimi.</summary>
    public string? Sent { get; set; }
}

public sealed class CustomerLoginViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public string? Notice { get; set; }
}

/// <summary>E-postadaki bağlantının onay sayfası: posta tarayıcıları bağlantıyı önceden açsa da durum ancak düğmeyle değişir.</summary>
public sealed class LinkConfirmViewModel
{
    public string Token { get; set; } = string.Empty;

    /// <summary>Doğrulamada parolalı hesap için parola alanı.</summary>
    public string? Password { get; set; }

    public bool Verify { get; set; }

    public bool AskPassword { get; set; }

    /// <summary>Bağlantı geçersizse form yerine hata ve yeni bağlantı isteme yolu.</summary>
    public bool Invalid { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class CustomerForgotViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    public string? Sent { get; set; }
}

public sealed class CustomerResetViewModel
{
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola gerekli.")]
    [StringLength(200)]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "Parolalar aynı değil.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class AddressFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Adrese kısa bir ad verin (Ev, İş).")]
    [StringLength(40)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ad soyad gerekli.")]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon gerekli.")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adres gerekli.")]
    [StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "İl gerekli.")]
    [StringLength(60)]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "İlçe gerekli.")]
    [StringLength(60)]
    public string District { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class AccountSettingsViewModel
{
    [StringLength(120)]
    public string? FullName { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    public bool MarketingConsent { get; set; }
}

public sealed class AccountPasswordViewModel
{
    [StringLength(200)]
    public string? CurrentPassword { get; set; }

    [Required(ErrorMessage = "Yeni parola gerekli.")]
    [StringLength(200)]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "Parolalar aynı değil.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Hesap sayfalarının ortak iskeleti: sekme, bildirim ve hata.</summary>
public abstract class AccountPageViewModel
{
    public Customer Customer { get; init; } = null!;
    public string? Notice { get; init; }
    public string? ErrorMessage { get; set; }
}

public sealed class AccountOrdersViewModel : AccountPageViewModel
{
    public IReadOnlyList<Order> Orders { get; init; } = [];
}

public sealed class AccountAddressesViewModel : AccountPageViewModel
{
    public IReadOnlyList<CustomerAddress> Addresses { get; init; } = [];
}

public sealed class AccountAddressFormViewModel : AccountPageViewModel
{
    public AddressFormViewModel Form { get; init; } = new();
}

public sealed class AccountFavoritesViewModel : AccountPageViewModel
{
    public IReadOnlyList<ProductCardVm> Cards { get; init; } = [];
}

public sealed class AccountReviewsViewModel : AccountPageViewModel
{
    public IReadOnlyList<ReviewRow> Reviews { get; init; } = [];
}

public sealed class AccountRegistriesViewModel : AccountPageViewModel
{
    public IReadOnlyList<GiftRegistry> Registries { get; init; } = [];
}

public sealed class AccountSettingsPageViewModel : AccountPageViewModel
{
    public AccountSettingsViewModel Form { get; init; } = new();
    public string? PasswordError { get; set; }
}

public sealed class AccountDeleteViewModel : AccountPageViewModel
{
}

/// <summary>Kalp düğmesi: ürün kartında ve ürün sayfasında. ReturnPath girişsiz ziyaretçinin girişten sonra döneceği yer.</summary>
public sealed record FavoriteButtonVm(int ProductId, string ProductName, string ReturnPath, bool Large = false);
