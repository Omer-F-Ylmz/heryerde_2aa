using System.ComponentModel.DataAnnotations;
using HerYerde.Business.Dtos;
using HerYerde.Entities.Concrete;

namespace HerYerde.Web.Models;

public sealed class GiftRegistryFormViewModel
{
    [Required(ErrorMessage = "Adınızı yazın.")]
    [StringLength(60, ErrorMessage = "Ad en çok 60 karakter olabilir.")]
    [Display(Name = "Adınız")]
    public string OwnerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cep telefonu gerekli.")]
    [StringLength(20)]
    [Display(Name = "Cep telefonu")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta yazın.")]
    [StringLength(200)]
    [Display(Name = "E-posta (isteğe bağlı)")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Etkinlik tarihini seçin.")]
    [Display(Name = "Düğün / taşınma tarihi")]
    public DateTime? EventDate { get; set; }

    [StringLength(500, ErrorMessage = "Mesaj en çok 500 karakter olabilir.")]
    [Display(Name = "Ziyaretçilere not (isteğe bağlı)")]
    public string? Message { get; set; }

    public bool IsPublic { get; set; } = true;
}

public sealed record GiftRegistryNewVm(GiftRegistryFormViewModel Form, string? Error);

/// <summary>Paylaşılan sayfa; Error hediye eklenemediyse doludur.</summary>
public sealed record GiftRegistryPageVm(GiftRegistryView View, string? Error = null)
{
    public int ProgressStep => RegistryProgress.Step(View.Received, View.Desired);
}

/// <summary>Liste satırı: Slug doluysa paylaşılan sayfada "Hediye et", Token doluysa yönetimde adet/çıkar.</summary>
public sealed record RegistryRowVm(GiftRegistryLine Line, string? Slug, Guid? Token);

public sealed record RegistrySearchResult(Product Product, string? ImageUrl, IReadOnlyList<ProductVariant> Variants);

public sealed record GiftRegistryManageVm(
    GiftRegistryView View,
    string PublicUrl,
    string ShareUrl,
    string SelfNoteUrl,
    string? Query,
    IReadOnlyList<RegistrySearchResult> Results,
    string? Notice,
    string? Error)
{
    public int ProgressStep => RegistryProgress.Step(View.Received, View.Desired);
}

public static class RegistryProgress
{
    /// <summary>Çubuğun genişliği sınıfla verilir (CSP satır içi stile izin vermez): yüzde ona yuvarlanır.</summary>
    public static int Step(int received, int desired)
        => desired <= 0 ? 0 : (int)Math.Round(Math.Min(received, desired) * 10d / desired) * 10;
}
