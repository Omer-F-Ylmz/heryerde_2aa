using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HerYerde.Tests.Web;

/// <summary>EF'in çalıştırdığı komutları sayar; bir isteğin kaç sorgu attığını ölçmek için.</summary>
public sealed class QueryCounter : ILoggerProvider
{
    private const string CommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";
    private const int CommandExecuted = 20101;

    private readonly List<string> _commands = [];

    /// <summary>Sayaç sıfırlandıktan sonra çalışan komutların metni.</summary>
    public IReadOnlyList<string> Commands
    {
        get
        {
            lock (_commands)
            {
                return _commands.ToList();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_commands)
            {
                return _commands.Count;
            }
        }
    }

    public void Reset()
    {
        lock (_commands)
        {
            _commands.Clear();
        }
    }

    ILogger ILoggerProvider.CreateLogger(string categoryName)
        => categoryName == CommandCategory ? new CountingLogger(this) : NullLogger.Instance;

    void IDisposable.Dispose()
    {
    }

    private sealed class CountingLogger(QueryCounter owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id != CommandExecuted)
            {
                return;
            }

            var text = formatter(state, exception);
            lock (owner._commands)
            {
                owner._commands.Add(text);
            }
        }
    }

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}

/// <summary>Sorgu sayan istemci üreten fabrika.</summary>
public sealed class CountingFactory : AdminWebFactory
{
    public QueryCounter Counter { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(Counter));
    }

    /// <summary>Sayacı sıfırlar, isteği atar, o istekte çalışan sorgu sayısını döner.</summary>
    public async Task<int> QueryCountAsync(HttpClient client, string url)
    {
        Counter.Reset();
        var response = await client.GetAsync(url);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        await response.Content.ReadAsStringAsync();
        return Counter.Count;
    }
}
