using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IProductReviewDal : IEntityRepository<ProductReview>
{
    /// <summary>Ürünün onaylı yorumları, en yeni önce.</summary>
    Task<List<ProductReview>> GetApprovedAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Tüm ürünlerden en yeni onaylı yorumlar, ürün adıyla.</summary>
    Task<List<ReviewRow>> GetLatestApprovedAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>Yönetim listesi: onay bekleyenler önce, sonra en yeniden eskiye.</summary>
    Task<List<ReviewRow>> GetForAdminAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>Üyenin yorumları (onay bekleyenler dahil), en yeni önce.</summary>
    Task<List<ReviewRow>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Sipariş numarası iptal edilmemiş bir siparişe ait ve satırlarından biri bu ürünse doğru.
    /// Satırın stok kodu varyantlı üründe varyant SKU'su, varyantsızda ürün slug'ıdır.</summary>
    Task<bool> IsPurchaseAsync(string orderNo, int productId, CancellationToken cancellationToken = default);
}
