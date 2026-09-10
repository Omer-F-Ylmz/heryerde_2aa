using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

/// <summary>Gecede bir, 30 gündür dokunulmamış anonim sepetleri siler; aksi halde sepet tablosu
/// hız sınırına takılmayan her ziyaretle büyümeye devam eder.</summary>
public sealed class CartCleanupHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<CartCleanupHostedService> _logger;

    public CartCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<CartCleanupHostedService> logger)
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
                var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
                var removed = await carts.PurgeStaleAsync(MaxAge, stoppingToken);
                _logger.LogInformation("Sepet temizliği: {Removed} bayat sepet silindi.", removed);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Sepet temizliği başarısız oldu; bir sonraki turda yeniden denenecek.");
            }
        }
    }
}
