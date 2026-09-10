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

    /// <summary>Ev ürününde adet takibi. NULL = takip yok; Giyim'de daima NULL, stok varyantta durur.</summary>
    public int? Stock { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Soft delete: dolu olan kayıtlar sorgulardan global filtreyle düşer.</summary>
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
