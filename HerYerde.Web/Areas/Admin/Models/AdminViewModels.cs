using System.ComponentModel.DataAnnotations;
using HerYerde.Business.Dtos;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

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

    [Display(Name = "Yayında")]
    public bool IsActive { get; set; }

    public string Slug { get; set; } = string.Empty;

    /// <summary>Giyim alanındaki ürün varyantsız yayına alınamaz; Ev alanında beden/renk boş kalır.</summary>
    public bool RequiresVariants { get; set; }

    public List<Category> Categories { get; set; } = [];
    public List<ProductVariant> Variants { get; set; } = [];
    public List<ProductImage> Images { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public sealed class ProductListViewModel
{
    public List<Product> Products { get; set; } = [];
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

    [Required(ErrorMessage = "Görsel adresi gerekli.")]
    [StringLength(500)]
    [Display(Name = "Görsel adresi")]
    public string Url { get; set; } = string.Empty;

    [Required(ErrorMessage = "Alternatif metin gerekli.")]
    [StringLength(200)]
    [Display(Name = "Alternatif metin")]
    public string Alt { get; set; } = string.Empty;

    [Range(0, 999)]
    [Display(Name = "Sıra")]
    public int SortOrder { get; set; }
}

public sealed class OrderListViewModel
{
    public List<Order> Orders { get; set; } = [];
    public OrderStatus? Status { get; set; }
    public string? Query { get; set; }
}

public sealed class OrderDetailViewModel
{
    public OrderDetail Detail { get; set; } = null!;
    public string? ErrorMessage { get; set; }

    /// <summary>Sıralı akışta bir sonraki adım; yoksa (teslim/iptal) buton çıkmaz.</summary>
    public OrderStatus? NextStatus => Detail.Order.Status switch
    {
        OrderStatus.Beklemede => OrderStatus.Onaylandi,
        OrderStatus.Onaylandi => OrderStatus.Kargoda,
        OrderStatus.Kargoda => OrderStatus.TeslimEdildi,
        _ => null
    };

    public bool CanCancel => Detail.Order.Status == OrderStatus.Beklemede;
}
