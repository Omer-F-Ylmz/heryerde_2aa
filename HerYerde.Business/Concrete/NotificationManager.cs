using HerYerde.Business.Abstract;
using HerYerde.Business.Notifications;
using HerYerde.Core.DataAccess;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.Extensions.Options;

namespace HerYerde.Business.Concrete;

public class NotificationManager : INotificationService
{
    /// <summary>Bu sayıya ulaşan kayıt bir daha denenmez.</summary>
    public const int MaxTries = 3;

    private const int BatchSize = 50;

    private readonly IOutboxMessageDal _outboxDal;
    private readonly INotificationSender _sender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NotificationSettings _notifications;
    private readonly ShippingSettings _shipping;
    private readonly ShopSettings _shop;
    private readonly TimeProvider _clock;

    public NotificationManager(
        IOutboxMessageDal outboxDal,
        INotificationSender sender,
        IUnitOfWork unitOfWork,
        IOptions<NotificationSettings> notifications,
        IOptions<ShippingSettings> shipping,
        IOptions<ShopSettings> shop,
        TimeProvider clock)
    {
        _outboxDal = outboxDal;
        _sender = sender;
        _unitOfWork = unitOfWork;
        _notifications = notifications.Value;
        _shipping = shipping.Value;
        _shop = shop.Value;
        _clock = clock;
    }

    public async Task QueueOrderPlacedAsync(
        Order order,
        IReadOnlyList<OrderItem> items,
        bool customer = true,
        bool store = true,
        CancellationToken cancellationToken = default)
    {
        if (customer && order.Email is { Length: > 0 } email)
        {
            var (subject, body) = NotificationTemplates.OrderPlaced(
                order,
                items,
                $"{_shop.BaseUrl}/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}",
                _shop.Iban);

            await QueueAsync(OutboxType.OrderPlaced, email, subject, body, cancellationToken);
        }

        if (store && _notifications.StoreTo is { Length: > 0 } storeTo)
        {
            var (subject, body) = NotificationTemplates.NewOrderForStore(
                order,
                items,
                $"{_shop.BaseUrl}/admin/orders/detail/{order.Id}");

            await QueueAsync(OutboxType.NewOrderForStore, storeTo, subject, body, cancellationToken);
        }
    }

    public async Task QueueOrderShippedAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Email is not { Length: > 0 } email)
        {
            return;
        }

        var (subject, body) = NotificationTemplates.OrderShipped(
            order,
            _shipping.TrackingUrl(order.Carrier, order.TrackingNo));

        await QueueAsync(OutboxType.OrderShipped, email, subject, body, cancellationToken);
    }

    public async Task QueuePaymentApprovedAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Email is not { Length: > 0 } email)
        {
            return;
        }

        var (subject, body) = NotificationTemplates.PaymentApproved(
            order,
            $"{_shop.BaseUrl}/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}");
        await QueueAsync(OutboxType.PaymentApproved, email, subject, body, cancellationToken);
    }

    public Task QueueAdminPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        var (subject, body) = NotificationTemplates.AdminPasswordReset($"{_shop.BaseUrl}/admin/auth/sifre-sifirla?t={token}");
        return QueueAsync(OutboxType.AdminPasswordReset, email, subject, body, cancellationToken);
    }

    public async Task QueueContactMessageAsync(ContactMessage message, CancellationToken cancellationToken = default)
    {
        if (_notifications.StoreTo is not { Length: > 0 } storeTo)
        {
            return;
        }

        var (subject, body) = NotificationTemplates.ContactMessage(message, $"{_shop.BaseUrl}/admin/mesajlar");
        await QueueAsync(OutboxType.ContactMessage, storeTo, subject, body, cancellationToken);
    }

    public async Task<NotificationDispatch> DispatchAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var due = await _outboxDal.DueAsync(now, BatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return new NotificationDispatch(0, 0, 0);
        }

        if (!_sender.IsConfigured)
        {
            // Ayar gelene kadar kayıtlar kuyrukta bekler; deneme sayısı artmaz.
            return new NotificationDispatch(0, 0, due.Count);
        }

        var sent = 0;
        var failed = 0;
        foreach (var message in due)
        {
            try
            {
                await _sender.SendAsync(message.To, message.Subject, message.Body, cancellationToken);
                message.Status = OutboxStatus.Gonderildi;
                message.SentAt = now;
                message.NextTryAt = null;
                sent++;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                message.TryCount++;
                message.Status = message.TryCount >= MaxTries ? OutboxStatus.Basarisiz : OutboxStatus.Bekliyor;
                // Üstel bekleme: 4 dk, sonra 16 dk.
                message.NextTryAt = now.AddMinutes(Math.Pow(4, message.TryCount));
                failed++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new NotificationDispatch(sent, failed, 0);
    }

    /// <summary>Gönderilmiş kayıtta süre SentAt'ten, başarısız kayıtta son denemenin bıraktığı NextTryAt'ten sayılır.</summary>
    public async Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default)
    {
        var limit = _clock.GetUtcNow().UtcDateTime - age;
        var stale = await _outboxDal.GetListAsync(
            m => (m.Status == OutboxStatus.Gonderildi && m.SentAt < limit)
                 || (m.Status == OutboxStatus.Basarisiz && m.NextTryAt < limit),
            cancellationToken);
        foreach (var message in stale)
        {
            _outboxDal.Delete(message);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }

    public async Task ForgetOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        // Sipariş postalarının konusu " · {sipariş no}" ile biter; tabloda sipariş kimliği tutulmaz.
        var suffix = " · " + order.OrderNo;
        foreach (var message in await _outboxDal.GetListAsync(m => m.Subject.EndsWith(suffix), cancellationToken))
        {
            _outboxDal.Delete(message);
        }
    }

    public async Task<TimeSpan?> OldestPendingAgeAsync(CancellationToken cancellationToken = default)
        => await _outboxDal.OldestPendingCreatedAtAsync(cancellationToken) is { } createdAt
            ? _clock.GetUtcNow().UtcDateTime - createdAt
            : null;

    private Task QueueAsync(string type, string to, string subject, string body, CancellationToken cancellationToken)
        => _outboxDal.AddAsync(
            new OutboxMessage
            {
                Type = type,
                To = to,
                Subject = subject,
                Body = body,
                Status = OutboxStatus.Bekliyor,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            },
            cancellationToken);
}
