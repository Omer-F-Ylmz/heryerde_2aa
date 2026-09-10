using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HerYerde.Tests.Web;

/// <summary>G15: şema yalnız üretim açılışında uygulanır; başarısızlık kritik loglanıp uygulamayı durdurur.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class DatabaseMigratorTests
{
    [Fact]
    public async Task Gelistirmede_veritabanina_hic_dokunulmaz()
    {
        var logger = new RecordingLogger();

        await DatabaseMigrator.ApplyAsync(new Env("Development"), EmptyProvider(), logger);

        Assert.Empty(logger.Levels);
    }

    [Fact]
    public async Task Uretimde_basarisizlik_kritik_loglanir_ve_disari_atilir()
    {
        var logger = new RecordingLogger();

        await Assert.ThrowsAnyAsync<Exception>(
            () => DatabaseMigrator.ApplyAsync(new Env("Production"), EmptyProvider(), logger));

        Assert.Contains(LogLevel.Critical, logger.Levels);
    }

    [Fact]
    public async Task Uretimde_bekleyen_gecis_kalmaz()
    {
        var services = new ServiceCollection();
        services.AddDbContext<HerYerdeContext>(options => options.UseSqlServer(TestDb.ConnectionString));
        await using var provider = services.BuildServiceProvider();

        await DatabaseMigrator.ApplyAsync(new Env("Production"), provider, new RecordingLogger());

        await using var context = TestDb.NewContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    /// <summary>Hiçbir servisi olmayan sağlayıcı: veritabanına dokunulursa istek patlar.</summary>
    private static ServiceProvider EmptyProvider() => new ServiceCollection().BuildServiceProvider();

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "HerYerde.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingLogger : ILogger<Program>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Levels.Add(logLevel);
    }
}
