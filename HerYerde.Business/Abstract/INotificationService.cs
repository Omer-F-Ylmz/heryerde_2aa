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

    /// <summary>İletişim formu mesajı: yalnız mağazaya; StoreTo boşsa atlanır. Kaydetmez.</summary>
    Task QueueContactMessageAsync(ContactMessage message, CancellationToken cancellationToken = default);

    /// <summary>Sırası gelen kayıtları gönderir; hata denemeyi artırır, üçüncüde kayıt başarısız olur.</summary>
    Task<NotificationDispatch> DispatchAsync(CancellationToken cancellationToken = default);
}

/// <summary>Bir dağıtım turunun sonucu; <see cref="Skipped"/> SMTP ayarsızken doludur.</summary>
public sealed record NotificationDispatch(int Sent, int Failed, int Skipped);
