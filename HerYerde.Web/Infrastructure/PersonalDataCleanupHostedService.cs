using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

/// <summary>Gecede bir, saklama süresi dolan kişisel veriyi siler: 30 günden eski gönderilmiş/başarısız posta, bir yıldan
/// eski iletişim mesajı ve etkinlik tarihinin üzerinden bir yıl geçmiş çeyiz listesi (saklama süreleri: docs/veri-envanteri.md).</summary>
public sealed class PersonalDataCleanupHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    public static readonly TimeSpan OutboxMaxAge = TimeSpan.FromDays(30);
    public static readonly TimeSpan ContactMessageMaxAge = TimeSpan.FromDays(365);
    public static readonly TimeSpan GiftRegistryMaxAge = TimeSpan.FromDays(365);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<PersonalDataCleanupHostedService> _logger;

    public PersonalDataCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<PersonalDataCleanupHostedService> logger)
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
                var mails = await scope.ServiceProvider.GetRequiredService<INotificationService>().PurgeOlderThanAsync(OutboxMaxAge, stoppingToken);
                var messages = await scope.ServiceProvider.GetRequiredService<IContactService>().PurgeOlderThanAsync(ContactMessageMaxAge, stoppingToken);
                var registries = await scope.ServiceProvider.GetRequiredService<IGiftRegistryService>().PurgeOlderThanAsync(GiftRegistryMaxAge, stoppingToken);
                _logger.LogInformation(
                    "Kişisel veri temizliği: {Mails} posta, {Messages} iletişim mesajı, {Registries} çeyiz listesi silindi.",
                    mails,
                    messages,
                    registries);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Kişisel veri temizliği başarısız oldu; bir sonraki turda yeniden denenecek.");
            }
        }
    }
}
