using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

/// <summary>Gecede bir, teslimden yedi gün geçmiş siparişlere "ürünlerinizi değerlendirin" daveti yazar.
/// İşlemsel posta: gövdesinde kampanya yok, İYS izni gerektirmez (bkz. docs/yasal-notlar.md).</summary>
public sealed class ReviewInviteHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReviewInviteHostedService> _logger;

    public ReviewInviteHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<ReviewInviteHostedService> logger)
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
                var queued = await scope.ServiceProvider.GetRequiredService<INotificationService>()
                    .QueueDueReviewInvitesAsync(stoppingToken);
                if (queued > 0)
                {
                    // Alıcı kişisel veri taşır; günlüğe yalnız sayı yazılır.
                    _logger.LogInformation("Değerlendirme daveti: {Queued} posta kuyruğa girdi.", queued);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Değerlendirme daveti turu başarısız oldu; bir sonraki turda yeniden denenecek.");
            }
        }
    }
}
