namespace HerYerde.Tests;

/// <summary>Testler sabit saatte çalışır; sipariş numarası ve arama kutusu örneği böylece tarihten bağımsız.</summary>
public static class TestClock
{
    public static readonly DateTime Now = new(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc);

    public static TimeProvider Fixed { get; } = new FixedTimeProvider(Now);

    /// <summary>İleri sarılabilir saat; üstel bekleme gibi zamana bağlı kuralları sınamak için.</summary>
    public static MovableTimeProvider Movable() => new(Now);

    private sealed class FixedTimeProvider(DateTime moment) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(moment, TimeSpan.Zero);
    }

    public sealed class MovableTimeProvider(DateTime moment) : TimeProvider
    {
        public DateTime Moment { get; private set; } = moment;

        public void Advance(TimeSpan span) => Moment += span;

        public override DateTimeOffset GetUtcNow() => new(Moment, TimeSpan.Zero);
    }
}
