using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Web.Infrastructure;

/// <summary>Yayın adımı: "--migrate" bekleyen geçişleri ortamdan bağımsız uygular ve çıkar, sunucu açılmaz. CD'de Plesk hedefinde
/// runner'daki yayın çıktısından uzak veritabanına, compose hedefinde "docker compose run --rm web --migrate" ile koşar.</summary>
public static class MigrateCommand
{
    public const string Argument = "--migrate";

    public static bool Requested(string[] args) => args.Contains(Argument);

    /// <summary>Uygulanan geçişlerin adları (şema güncelse boş).</summary>
    public static async Task<IReadOnlyList<string>> RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HerYerdeContext>();
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        await context.Database.MigrateAsync(cancellationToken);
        return pending;
    }
}
