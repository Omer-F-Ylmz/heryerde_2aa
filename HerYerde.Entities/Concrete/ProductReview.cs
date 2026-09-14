using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Ürün yorumu; yönetici onaylayana kadar vitrinde görünmez.</summary>
public class ProductReview : IEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>1-5; veritabanında check kısıtıyla da korunur.</summary>
    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Doğrulanmış alıcı: yalnız bu ürünü içeren gerçek bir siparişin numarasıysa yazılır.</summary>
    public string? OrderNo { get; set; }
}
