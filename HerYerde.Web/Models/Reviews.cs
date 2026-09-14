using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HerYerde.Entities.Concrete;

namespace HerYerde.Web.Models;

/// <summary>Onaylı yorumların ortalaması (bir ondalık) ve sayısı.</summary>
public sealed record RatingSummary(decimal Average, int Count)
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static RatingSummary? From(IReadOnlyCollection<ProductReview> approved)
        => approved.Count == 0
            ? null
            : new RatingSummary(Math.Round((decimal)approved.Average(r => r.Rating), 1, MidpointRounding.AwayFromZero), approved.Count);

    /// <summary>"4,5"</summary>
    public string AverageText => Average.ToString("0.0", Turkish);

    /// <summary>Yıldız çizimi için en yakın tam sayı.</summary>
    public int Stars => (int)Math.Round(Average, MidpointRounding.AwayFromZero);
}

/// <summary>FRONT kiti: yorum formu. Puan radyo düğmesiyle seçilir; sipariş numarası doğrulanmış alıcı rozeti içindir.</summary>
public sealed class ReviewFormViewModel
{
    [Required(ErrorMessage = "Adınızı yazın.")]
    [StringLength(60, ErrorMessage = "Ad en fazla 60 karakter olabilir.")]
    [Display(Name = "Adınız")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "1 ile 5 arasında puan seçin.")]
    [Display(Name = "Puanınız")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Yorumunuzu yazın.")]
    [StringLength(1000, ErrorMessage = "Yorum en fazla 1000 karakter olabilir.")]
    [Display(Name = "Yorumunuz")]
    public string Comment { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Sipariş numarası (isteğe bağlı)")]
    public string? OrderNo { get; set; }
}

/// <summary>Ürün sayfasının yorum bölümü: onaylı yorumlar, özet, form ve gönderim sonrası bildirim.</summary>
public sealed record ReviewSectionVm(
    string ProductSlug,
    IReadOnlyList<ProductReview> Reviews,
    RatingSummary? Summary,
    ReviewFormViewModel Form,
    string? Notice);
