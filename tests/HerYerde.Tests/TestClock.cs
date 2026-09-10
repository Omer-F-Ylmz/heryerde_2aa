namespace HerYerde.Tests;

/// <summary>Testler sabit saatte çalışır; sipariş numarası ve arama kutusu örneği böylece tarihten bağımsız.</summary>
public static class TestClock
{
    public static readonly DateTime Now = new(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc);

    public static TimeProvider Fixed { get; } = new FixedTimeProvider(Now);

    private sealed class FixedTimeProvider(DateTime moment) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(moment, TimeSpan.Zero);
    }
}
