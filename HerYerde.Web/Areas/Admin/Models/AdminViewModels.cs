using System.ComponentModel.DataAnnotations;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using HerYerde.Web.Models;

namespace HerYerde.Web.Areas.Admin.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta yazın.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Mevcut parola gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola gerekli.")]
    [StringLength(200, MinimumLength = 10, ErrorMessage = "Yeni parola en az 10 karakter olmalı.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parolayı tekrar yazın.")]
    [Compare(nameof(NewPassword), ErrorMessage = "İki parola aynı değil.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public bool Changed { get; set; }

    /// <summary>Geçici parolayla girildi; sayfa başka bölüme geçmeden önce parola ister.</summary>
    public bool MustChange { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta yazın.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Gönderim sonrası tek tip bilgi metni; e-postanın kayıtlı olup olmadığını söylemez.</summary>
    public string? Sent { get; set; }
}

public sealed class ResetPasswordViewModel
{
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola gerekli.")]
    [StringLength(200, MinimumLength = 10, ErrorMessage = "Yeni parola en az 10 karakter olmalı.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parolayı tekrar yazın.")]
    [Compare(nameof(NewPassword), ErrorMessage = "İki parola aynı değil.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class SecondFactorViewModel
{
    [Display(Name = "Doğrulama kodu")]
    public string? Code { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class TwoFactorSetupViewModel
{
    public bool Enabled { get; set; }

    /// <summary>Kurulumdaki anahtar (QR okutulamazsa elle yazılır); açıkken gösterilmez.</summary>
    public string? Secret { get; set; }

    public string? QrDataUri { get; set; }

    /// <summary>Yalnız açıldığı yanıtta dolu; bir daha gösterilmez.</summary>
    public IReadOnlyList<string> RecoveryCodes { get; set; } = [];

    public string? ErrorMessage { get; set; }
}

public sealed class CategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad gerekli.")]
    [StringLength(120)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Üst kategori")]
    public int? ParentId { get; set; }

    [Range(0, 999)]
    [Display(Name = "Sıra")]
    public int SortOrder { get; set; }

    [Display(Name = "Yayında")]
    public bool IsActive { get; set; } = true;

    public string Slug { get; set; } = string.Empty;
    public bool IsRoot { get; set; }

    /// <summary>Yeni görsel (isteğe bağlı); yüklenince eskisi silinir.</summary>
    [Display(Name = "Görsel")]
    public IFormFile? Image { get; set; }

    /// <summary>Kayıtlı 16:9 görsel; formda önizleme.</summary>
    public string? ImageUrl { get; set; }
    public List<Category> Parents { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class CategoryListViewModel
{
    public List<Category> Categories { get; set; } = [];
    public Dictionary<int, string> RootNames { get; set; } = [];
    public Dictionary<int, int> ProductCounts { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad gerekli.")]
    [StringLength(200)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Ad standarttan sapıyorsa önerilen yazım; uyuyorsa null. Yalnız ipucudur, kayıt engellenmez.</summary>
    public string? SuggestedName
        => ProductRules.NormalizeName(Name) is var suggested && suggested.Length > 0 && suggested != Name ? suggested : null;

    [Required(ErrorMessage = "Açıklama gerekli.")]
    [Display(Name = "Açıklama")]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Kategori seçin.")]
    [Display(Name = "Kategori")]
    public int CategoryId { get; set; }

    [Range(0, 999999)]
    [Display(Name = "Fiyat")]
    public decimal Price { get; set; }

    [Range(0, 999999)]
    [Display(Name = "Kampanya fiyatı")]
    public decimal? CampaignPrice { get; set; }

    [StringLength(40)]
    [Display(Name = "Kampanya etiketi")]
    public string? CampaignLabel { get; set; }

    [Display(Name = "Kampanya bitişi")]
    public DateTime? CampaignEndsAt { get; set; }

    [StringLength(60)]
    [Display(Name = "Ölçü (isteğe bağlı)")]
    public string? Dimensions { get; set; }

    [StringLength(30)]
    [Display(Name = "1. eksen adı")]
    public string? VariantAxis1Label { get; set; }

    [StringLength(30)]
    [Display(Name = "2. eksen adı")]
    public string? VariantAxis2Label { get; set; }

    [Display(Name = "Stok (boş = takip yok)")]
    [Range(0, int.MaxValue, ErrorMessage = "Stok negatif olamaz.")]
    public int? Stock { get; set; }

    [Display(Name = "Hediye")]
    public GiftMode GiftMode { get; set; }

    [Display(Name = "Hediye edilen ürün")]
    public int? GiftProductId { get; set; }

    [Range(1, 99, ErrorMessage = "Hediye adedi en az 1 olmalı.")]
    [Display(Name = "Hediye adedi")]
    public int GiftQty { get; set; } = 1;

    [Display(Name = "Yayında")]
    public bool IsActive { get; set; }

    public string Slug { get; set; } = string.Empty;

    /// <summary>Giyim alanındaki ürün varyantsız yayına alınamaz; Ev alanında varyant isteğe bağlıdır.</summary>
    public bool RequiresVariants { get; set; }

    public List<Category> Categories { get; set; } = [];

    /// <summary>Hediye olarak seçilebilecek ürünler; kendisi listede yer almaz.</summary>
    public List<Product> GiftProducts { get; set; } = [];
    public List<ProductVariant> Variants { get; set; } = [];
    public List<ProductImage> Images { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class ProductListViewModel
{
    public List<Product> Products { get; set; } = [];

    /// <summary>"Fiyat eksik" süzgeci açıkken yalnız ithalden gelen fiyatsız ürünler listelenir.</summary>
    public bool PriceMissingOnly { get; set; }
    public Dictionary<int, string> CategoryNames { get; set; } = [];
    public Dictionary<int, int> StockTotals { get; set; } = [];
    public DateTime Now { get; set; }
}

public sealed class VariantFormViewModel
{
    public int ProductId { get; set; }

    [StringLength(16)]
    [Display(Name = "Beden")]
    public string? Size { get; set; }

    [StringLength(60)]
    [Display(Name = "Renk")]
    public string? Color { get; set; }

    [Required(ErrorMessage = "Stok kodu gerekli.")]
    [StringLength(60)]
    [Display(Name = "Stok kodu")]
    public string Sku { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Stok negatif olamaz.")]
    [Display(Name = "Stok")]
    public int Stock { get; set; }
}

public sealed class ImageFormViewModel
{
    public int ProductId { get; set; }

    /// <summary>Yüklenen dosyalar; adres kullanıcıdan alınmaz, sunucu üretir.</summary>
    public List<IFormFile>? Files { get; set; }

    [Required(ErrorMessage = "Alternatif metin gerekli.")]
    [StringLength(200)]
    [Display(Name = "Alternatif metin")]
    public string Alt { get; set; } = string.Empty;

    [Range(0, 999)]
    [Display(Name = "Sıra")]
    public int SortOrder { get; set; }
}

public sealed class ManualOrderFormViewModel
{
    /// <summary>Formdaki boş kalem satırı sayısı; boş bırakılan satır yok sayılır.</summary>
    public const int LineCount = 6;

    [Display(Name = "Ad soyad")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Cep telefonu")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "E-posta (isteğe bağlı)")]
    public string? Email { get; set; }

    [Display(Name = "Adres")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "İl")]
    public string City { get; set; } = string.Empty;

    [Display(Name = "İlçe")]
    public string District { get; set; } = string.Empty;

    [Display(Name = "Not")]
    public string? Note { get; set; }

    [Display(Name = "Kanal")]
    public OrderSource Source { get; set; } = OrderSource.WhatsApp;

    [Display(Name = "Ödeme")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.KapidaOdeme;

    [Display(Name = "Kargo ücreti (boş = kurala göre)")]
    public decimal? ShippingFeeOverride { get; set; }

    [Display(Name = "Müşteriye sipariş postası gönder")]
    public bool NotifyCustomer { get; set; }

    [Display(Name = "Ön bilgilendirme ve sözleşme iletildi, müşteri teyit etti")]
    public bool ConsentConfirmed { get; set; }

    public List<ManualOrderLineForm> Lines { get; set; } = [];

    public string? ErrorMessage { get; set; }
}

public sealed class ManualOrderLineForm
{
    public string? Code { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class OrderEditViewModel
{
    public Order Order { get; set; } = null!;

    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Note { get; set; }
    public List<OrderEditItemForm> Items { get; set; } = [];

    /// <summary>Formda satır adı ve birim fiyatı; POST'ta gelmez, yeniden okunur.</summary>
    public IReadOnlyList<OrderItem> Lines { get; set; } = [];

    public string? ErrorMessage { get; set; }
}

public sealed class OrderEditItemForm
{
    public int Id { get; set; }
    public int Quantity { get; set; }
}

public sealed class OrderListViewModel
{
    public List<Order> Orders { get; set; } = [];
    public OrderStatus? Status { get; set; }
    public string? Query { get; set; }

    /// <summary>"Faturasız teslim edilenler" süzgeci açık.</summary>
    public bool Uninvoiced { get; set; }

    /// <summary>Arama kutusunun örnek metni; bugünün numarası olsun diye saatten üretilir.</summary>
    public string SampleOrderNo { get; set; } = string.Empty;
}

public sealed class OrderDetailViewModel
{
    public OrderDetail Detail { get; set; } = null!;
    public string? ErrorMessage { get; set; }

    /// <summary>Ayarlardaki kargo firmaları; "Kargoya verdim" formunun seçenekleri.</summary>
    public IReadOnlyList<string> Carriers { get; set; } = [];

    public string? TrackingUrl { get; set; }

    /// <summary>Müşteriyle hızlı temas: ad, sipariş numarası ve durum önden dolu.</summary>
    public string WhatsAppUrl => WhatsAppLink.For(
        Detail.Order.Phone,
        $"Merhaba {Detail.Order.FullName}, {Detail.Order.OrderNo} numaralı siparişiniz: "
        + OrderLabels.For(Detail.Order.Status) + ".");

    /// <summary>Sıralı akışta bir sonraki adım; yoksa (teslim/iptal) buton çıkmaz.</summary>
    public OrderStatus? NextStatus => HerYerde.Business.Rules.OrderRules.Next(Detail.Order.Status);

    public bool CanCancel => Detail.Order.Status == OrderStatus.Beklemede && !CanRefund;

    /// <summary>Kartla ödenmiş sipariş iptal düğmesiyle değil iadeyle kapanır: para da geri gider.</summary>
    public bool CanRefund => Detail.Payment?.Status == PaymentStatus.Basarili && HerYerde.Business.Rules.OrderRules.CanRefund(Detail.Order.Status);

    public bool CanEdit => HerYerde.Business.Rules.OrderRules.CanEdit(Detail.Order.Status);

    /// <summary>Bekleyen havale siparişi: bildirim gelmiş olsun olmasın yönetici hesabı görünce onaylar.</summary>
    public bool CanApproveTransfer => Detail.Order.PaymentMethod == PaymentMethod.HavaleEft && Detail.Order.Status == OrderStatus.Beklemede;

    /// <summary>Sıradaki adım kargoysa firma ve takip numarası istenir.</summary>
    public bool NeedsShipment => NextStatus == OrderStatus.Kargoda;

    /// <summary>Kişisel veri yalnız kapanmış (teslim/iptal) siparişte anonimleştirilir.</summary>
    public bool CanAnonymize => HerYerde.Business.Rules.OrderRules.CanAnonymize(Detail.Order.Status);
}

public sealed class ProductImportViewModel
{
    public ImportPreview? Preview { get; set; }

    /// <summary>Önizlenen dosyanın gizli depodaki adı; onay bununla aynı dosyayı yeniden okur.</summary>
    public string? Token { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class ReportViewModel
{
    public SalesReport Report { get; set; } = null!;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public ReportPeriod Period { get; set; }

    /// <summary>Sorgu dizesi: CSV bağlantısı aynı süzgeçle iner.</summary>
    public string Query => $"baslangic={From:yyyy-MM-dd}&bitis={To:yyyy-MM-dd}&donem={Period.ToString().ToLowerInvariant()}";
}

public sealed class AuditListViewModel
{
    public AdminAuditPage Page { get; set; } = null!;
}
