namespace HerYerde.Web.Infrastructure;

/// <summary>30 sn'de bir biriken analitik kaydını yazar; İstanbul'da gün değişince (ve açılıştaki ilk turda) 90 günden eski ham
/// kaydı günlük özete toplayıp siler. Kapanırken bekleyen kayıt da yazılır.</summary>
public sealed class AnalyticsHostedService(AnalyticsWriter writer, TimeProvider clock, ILogger<AnalyticsHostedService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private DateOnly? _rolledUpOn;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await writer.FlushAsync(stoppingToken);
                var today = HerYerde.Web.Models.IstanbulTime.Today(clock);
                if (_rolledUpOn != today)
                {
                    var removed = await writer.RollupAsync(stoppingToken);
                    _rolledUpOn = today;
                    logger.LogInformation("Analitik özetleme: {Removed} ham kayıt günlük özete toplanıp silindi.", removed);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Analitik yazımı başarısız oldu; bir sonraki turda yeniden denenecek.");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            await writer.FlushAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Kapanışta bekleyen analitik kaydı yazılamadı.");
        }
    }
}
