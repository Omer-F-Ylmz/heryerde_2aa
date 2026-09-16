using HerYerde.Core.Entities;
using HerYerde.Entities.Enums;

namespace HerYerde.Entities.Concrete;

/// <summary>İndirim kuponu. Kullanım limitleri iptal edilmemiş siparişlerin coupon_code alanından sayılır;
/// kişi başı limit telefon ya da e-posta eşleşmesine bakar.</summary>
public class Coupon : IEntity
{
    public int Id { get; set; }

    /// <summary>Büyük harfe çevrilmiş kod; tabloda tekil.</summary>
    public string Code { get; set; } = string.Empty;

    public CouponKind Kind { get; set; }

    /// <summary>Yüzde kuponunda oran, tutar kuponunda TL; kargo bedavada kullanılmaz.</summary>
    public decimal Value { get; set; }

    /// <summary>Bu ara toplamın altındaki sepette geçmez; 0 ise alt sınır yok.</summary>
    public decimal MinSubtotal { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    /// <summary>Toplam kaç siparişte kullanılabilir; boşsa sınırsız.</summary>
    public int? TotalLimit { get; set; }

    /// <summary>Aynı telefon ya da e-posta kaç kez kullanabilir; boşsa sınırsız.</summary>
    public int? PerPersonLimit { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
