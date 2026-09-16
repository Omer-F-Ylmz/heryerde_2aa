using HerYerde.Core.Entities;
using HerYerde.Entities.Enums;

namespace HerYerde.Entities.Concrete;

public class Product : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }

    /// <summary>Markasız ürün (el yapımı, markasız ithal) boş kalır.</summary>
    public int? BrandId { get; set; }
    public decimal Price { get; set; }

    /// <summary>Doluysa fiyattan küçük olmak zorunda (ck_product_campaign_price).</summary>
    public decimal? CampaignPrice { get; set; }
    public string? CampaignLabel { get; set; }
    public DateTime? CampaignEndsAt { get; set; }

    /// <summary>"1 alana 1 hediye"; yalnız kampanya süresi içinde geçerli.</summary>
    public GiftMode GiftMode { get; set; }

    /// <summary>GiftMode = BaskaUrun iken hediye edilen ürün.</summary>
    public int? GiftProductId { get; set; }

    /// <summary>Hediye adedi; en az 1.</summary>
    public int GiftQty { get; set; } = 1;

    /// <summary>Varyantsız ürünün adet takibi. NULL = takip yok; varyantlı üründe (Giyim daima) NULL, stok varyantta durur.</summary>
    public int? Stock { get; set; }

    /// <summary>Varyantın birinci ekseninin (ProductVariant.Size) vitrindeki adı, ör. "Beden", "Boy"; boşsa "Beden".</summary>
    public string? VariantAxis1Label { get; set; }

    /// <summary>İkinci eksenin (ProductVariant.Color) adı, ör. "Renk", "Desen"; boşsa "Renk".</summary>
    public string? VariantAxis2Label { get; set; }

    /// <summary>Serbest metin ölçü (ör. "70x70 cm"); yalnız ürün detayında "Ölçü" satırı olarak görünür.</summary>
    public string? Dimensions { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Ana sayfadaki "Öne çıkanlar" rafında; işaretli ürün yoksa raf yeni gelenlere düşer.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>Öne çıkanlar rafındaki sıra; küçük olan önce, eşitlikte en yeni önce.</summary>
    public int FeaturedOrder { get; set; }

    /// <summary>Soft delete: dolu olan kayıtlar sorgulardan global filtreyle düşer.</summary>
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
