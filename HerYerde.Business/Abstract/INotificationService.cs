using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

/// <summary>Sipariş e-postalarını kuyruğa yazar ve arka planda dağıtır.</summary>
public interface INotificationService
{
    /// <summary>Sipariş alındı: müşteriye (e-postası varsa) ve mağazaya. Kaydetmez; çağıranın işlemine yazılır.
    /// Kartlı siparişte mağaza postası sipariş anında, müşteri postası ödeme onayında ayrı ayrı istenir.</summary>
    Task QueueOrderPlacedAsync(
        Order order,
        IReadOnlyList<OrderItem> items,
        bool customer = true,
        bool store = true,
        CancellationToken cancellationToken = default);

    /// <summary>Sipariş kargoya verildi: yalnız müşteriye. Kaydetmez.</summary>
    Task QueueOrderShippedAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Havale onaylandı: yalnız müşteriye (e-postası varsa). Kaydetmez.</summary>
    Task QueuePaymentApprovedAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>İade/değişim talebi onaylandı: müşteriye (e-postası varsa) iade adresi ve kargo bilgisi. Kaydetmez.</summary>
    Task QueueReturnApprovedAsync(Order order, ReturnRequest request, CancellationToken cancellationToken = default);

    /// <summary>İade/değişim talebi reddedildi: müşteriye (e-postası varsa) gerekçe. Kaydetmez.</summary>
    Task QueueReturnRejectedAsync(Order order, ReturnRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fatura yüklendi: müşteriye (e-postası varsa) token'lı indirme bağlantısı. Kaydetmez.</summary>
    Task QueueInvoiceReadyAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Teslimden bu yana yeterince geçmiş siparişlere değerlendirme daveti kuyruğa yazar ve hepsini
    /// işlenmiş işaretler (e-postasızlar da: her gece yeniden taranmasınlar). Kuyruğa giren posta sayısını döner.
    /// Kaydeder.</summary>
    Task<int> QueueDueReviewInvitesAsync(CancellationToken cancellationToken = default);

    /// <summary>Çeyiz listesi açıldı: sahibine (e-postası varsa) yönetim ve paylaşım bağlantısı. Kaydetmez.</summary>
    Task QueueGiftRegistryCreatedAsync(GiftRegistry registry, CancellationToken cancellationToken = default);

    /// <summary>Listeden hediye alındı: sahibine (e-postası varsa) alanın adı ve ürünler. Konu sipariş numarasıyla biter:
    /// sipariş anonimleştirilince bu posta da silinir. Kaydetmez.</summary>
    Task QueueGiftRegistryPurchaseAsync(GiftRegistry registry, Order order, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken = default);

    /// <summary>Yönetici parola sıfırlama bağlantısı; anahtar yalnız postada açık, veritabanında özeti durur. Kaydetmez.</summary>
    Task QueueAdminPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default);

    /// <summary>İletişim formu mesajı: yalnız mağazaya; StoreTo boşsa atlanır. Kaydetmez.</summary>
    Task QueueContactMessageAsync(ContactMessage message, CancellationToken cancellationToken = default);

    /// <summary>Sırası gelen kayıtları gönderir; hata denemeyi artırır, üçüncüde kayıt başarısız olur.</summary>
    Task<NotificationDispatch> DispatchAsync(CancellationToken cancellationToken = default);

    /// <summary>Gönderilmiş ya da vazgeçilmiş postaları saklama süresi dolunca siler; bekleyene dokunmaz.</summary>
    Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default);

    /// <summary>Siparişin tüm postalarını (alıcı, ad, token'lı bağlantı) siler; anonimleştirmede kullanılır. Kaydetmez.</summary>
    Task ForgetOrderAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Bekleyen en eski postanın kuyrukta geçirdiği süre; bekleyen yoksa null.</summary>
    Task<TimeSpan?> OldestPendingAgeAsync(CancellationToken cancellationToken = default);
}

/// <summary>Bir dağıtım turunun sonucu; <see cref="Skipped"/> SMTP ayarsızken doludur.</summary>
public sealed record NotificationDispatch(int Sent, int Failed, int Skipped);
