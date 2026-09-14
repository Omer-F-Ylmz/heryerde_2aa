using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

/// <summary>Dakikada bir sipariş postalarını gönderir. Gönderim istek akışının dışında olduğu için
/// SMTP yavaşlarsa ya da düşerse sipariş alma etkilenmez.</summary>
public sealed class OutboxHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxHostedService> _logger;

    public OutboxHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<OutboxHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _clock);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var result = await notifications.DispatchAsync(stoppingToken);
                if (result.Skipped > 0)
                {
                    _logger.LogWarning(
                        "Bildirim gönderimi atlandı: SMTP ayarı yok, {Skipped} kayıt kuyrukta bekliyor.",
                        result.Skipped);
                }
                else if (result.Sent > 0 || result.Failed > 0)
                {
                    // Alıcı ve gövde kişisel veri taşır; günlüğe yalnız sayılar yazılır.
                    _logger.LogInformation(
                        "Bildirim turu: {Sent} gönderildi, {Failed} başarısız.",
                        result.Sent,
                        result.Failed);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Bildirim turu başarısız oldu; bir sonraki turda yeniden denenecek.");
            }
        }
    }
}
