using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Web.Infrastructure;

/// <summary>Üretimde şema açılışta uygulanır. Geliştirme ve testler kendi veritabanını kurduğu için
/// orada hiç dokunulmaz; uygulanamayan geçişte uygulama yarı şemayla ayakta kalmaz.</summary>
public static class DatabaseMigrator
{
    public static async Task ApplyAsync(
        IHostEnvironment environment,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        try
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<HerYerdeContext>();
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Veritabanı şeması güncel.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Veritabanı geçişleri uygulanamadı; uygulama başlatılmıyor.");
            throw;
        }
    }
}
