using HerYerde.Business.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HerYerde.Web.Infrastructure;

/// <summary>/health/ready: veritabanına bağlanılır, uploads klasörüne yazılabilir ve bekleyen en eski posta 10 dakikadan
/// genç. /health yalnız sürecin ayakta olduğunu söyler; bu denetim trafiğin gerçekten karşılanabildiğini.
/// Hangi koşulun bozulduğu yanıt gövdesine değil, sağlık denetimi loguna yazılır.</summary>
public sealed class ReadinessCheck(
    HerYerdeContext context,
    IProductImageStorage storage,
    INotificationService notifications) : IHealthCheck
{
    public const string Tag = "ready";

    public static readonly TimeSpan MaxOutboxDelay = TimeSpan.FromMinutes(10);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken cancellationToken = default)
    {
        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy("Veritabanına bağlanılamıyor.");
        }

        try
        {
            Directory.CreateDirectory(storage.UploadsPath);
            var probe = Path.Combine(storage.UploadsPath, $".hazirlik-{Guid.NewGuid():n}");
            await File.WriteAllBytesAsync(probe, [], cancellationToken);
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy("Uploads klasörüne yazılamıyor.", exception);
        }

        return await notifications.OldestPendingAgeAsync(cancellationToken) is { } age && age >= MaxOutboxDelay
            ? HealthCheckResult.Unhealthy($"Bekleyen en eski posta {age.TotalMinutes:F0} dakikadır kuyrukta.")
            : HealthCheckResult.Healthy();
    }
}
