using HerYerde.Core.Entities;

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

    public bool IsActive { get; set; }

    /// <summary>Soft delete: dolu olan kayıtlar sorgulardan global filtreyle düşer.</summary>
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
