using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

/// <summary>Gecede bir, bir yıldan eski denetim izini siler (saklama süresi: docs/veri-envanteri.md).</summary>
public sealed class AuditLogCleanupHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(365);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuditLogCleanupHostedService> _logger;

    public AuditLogCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<AuditLogCleanupHostedService> logger)
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
                var audit = scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
                var removed = await audit.PurgeOlderThanAsync(MaxAge, stoppingToken);
                _logger.LogInformation("Denetim izi temizliği: {Removed} satır silindi.", removed);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Denetim izi temizliği başarısız oldu; bir sonraki turda yeniden denenecek.");
            }
        }
    }
}
