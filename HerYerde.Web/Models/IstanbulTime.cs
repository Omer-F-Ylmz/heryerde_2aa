using System.Globalization;

namespace HerYerde.Web.Models;

public static class IstanbulTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>Veritabanındaki UTC an → İstanbul saati: 2026-09-01 09:30Z → "01.09.2026 12:30".</summary>
    public static string Format(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone)
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>İstanbul'da günün başladığı UTC an; tarih süzgeçleri [gün başı, ertesi gün başı) aralığıyla sorar.</summary>
    public static DateTime StartOfDayUtc(DateOnly day)
        => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);

    public static DateOnly Today(TimeProvider clock)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, Zone));
}
