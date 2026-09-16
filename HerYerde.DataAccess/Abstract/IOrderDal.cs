using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.DataAccess.Abstract;

public interface IOrderDal : IEntityRepository<Order>
{
    /// <summary>Kuponun iptal edilmemiş siparişlerdeki kullanım sayısı; telefon verilirse yalnız o kişinin
    /// (telefon ya da e-posta eşleşen) kullanımları.</summary>
    Task<int> CouponUsageAsync(string code, string? phone, string? email, CancellationToken cancellationToken = default);

    /// <summary>Değerlendirme daveti sırası gelmiş siparişler: teslim edilmiş, daveti henüz işlenmemiş ve
    /// teslimden <paramref name="moment"/> anına kadar yeterince zaman geçmiş olanlar; izlenir.</summary>
    Task<List<Order>> DueForReviewInviteAsync(DateTime moment, int take, CancellationToken cancellationToken = default);

    /// <summary>Sipariş numarasının sıra kısmını veritabanı SEQUENCE'ından alır; eşzamanlı isteklerde
    /// her çağrı farklı değer döner, sıra gün başında sıfırlanmaz.</summary>
    Task<long> NextOrderSequenceAsync(CancellationToken cancellationToken = default);

    /// <summary>Siparişi yalnız hâlâ <paramref name="from"/> durumundaysa <paramref name="to"/> yapar; tek koşullu UPDATE, eşzamanlı
    /// ikinci istek 0 alır (ör. müşteri iptalinde stok iki kez dönmesin).</summary>
    Task<int> TryChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to, CancellationToken cancellationToken = default);

    /// <summary>Pano sayaçları tek sorguda: [todayStart, ∞) ve [weekStart, ∞) aralığında iptal dışı sipariş sayısı ve kasaya giren
    /// ciro (kart çekimi başarılı, havale onaylı, kapıda/elden teslim edilmiş), bekleyen havale bildirimi, iade talebi, onaysız yorum,
    /// okunmamış iletişim mesajı.</summary>
    Task<DashboardCounts> DashboardCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken = default);

    /// <summary>Yönetimde hiç açılmamış sipariş sayısı.</summary>
    Task<int> UnseenCountAsync(CancellationToken cancellationToken = default);
}
