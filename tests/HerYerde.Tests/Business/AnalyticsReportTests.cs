using System.Text.Json;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Business;

/// <summary>D16 A1: analitik raporu — huni ve ziyaret sayıları elle hesaplananla eşit; 90 günden eski ham kayıt günlük
/// özete toplanıp silinir, rapor değişmez.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AnalyticsReportTests : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 1, 15);

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Huni_ziyaret_kaynak_ve_urun_sayilari_elle_hesaplanan_ile_esit()
    {
        await using (var context = TestDb.NewContext())
        {
            var day = new DateOnly(2026, 1, 14);
            // /urun/a: aynı yarım saatte aynı kaynaktan iki görüntüleme = 1 ziyaret, öbür yarım saatte 1 ziyaret daha.
            context.PageViews.AddRange(
                View(day, "/urun/a", "instagram.com", hour: 10, halfHour: false),
                View(day, "/urun/a", "instagram.com", hour: 10, halfHour: false),
                View(day, "/urun/a", "instagram.com", hour: 10, halfHour: true),
                View(day, "/urun/b", null, hour: 11, halfHour: false),
                View(day, "/ev", null, hour: 11, halfHour: false),
                Event(day, AnalyticsEvent.AddToCart),
                Event(day, AnalyticsEvent.AddToCart),
                Event(day, AnalyticsEvent.Checkout),
                Event(day, AnalyticsEvent.Order),
                Event(day, AnalyticsEvent.WhatsApp),
                Event(day, AnalyticsEvent.Instagram),
                Event(day, AnalyticsEvent.Search),
                View(Today, "/urun/b", null, hour: 9, halfHour: false),
                Event(Today, AnalyticsEvent.Order));
            // Saklama süresinin gerisindeki gün yalnız özette durur.
            context.AnalyticsDailies.AddRange(
                new AnalyticsDaily { Day = new DateTime(2025, 10, 1), Event = AnalyticsEvent.View, Path = "/urun/a", Source = "instagram.com", Device = "mobil", Views = 4, Visits = 3 },
                new AnalyticsDaily { Day = new DateTime(2025, 10, 1), Event = AnalyticsEvent.Order, Path = "/odeme", Source = "", Device = "mobil", Views = 1, Visits = 0 });
            await context.SaveChangesAsync();
        }

        await using var job = TestDb.NewContext();
        var report = await TestData.NewAnalyticsManager(job).GetReportAsync(new DateOnly(2025, 9, 1), Today);

        Assert.Equal(10, report.TotalViews);
        Assert.Equal(8, report.TotalVisits);
        Assert.Equal(new(9, 2, 1, 3), report.Funnel);
        Assert.Equal(1, report.WhatsAppClicks);
        Assert.Equal(1, report.InstagramClicks);
        Assert.Equal(1, report.Searches);
        Assert.Equal([new(new DateOnly(2025, 10, 1), 4, 3), new(new DateOnly(2026, 1, 14), 5, 4), new(Today, 1, 1)], report.Days);
        Assert.Equal([new("/urun/a", 7), new("/urun/b", 2)], report.TopProducts);
        Assert.Equal([new("instagram.com", 7), new("", 3)], report.Sources);
    }

    [Fact]
    public async Task Doksan_birinci_gundeki_ham_kayit_ozete_toplanip_silinir_rapor_degismez()
    {
        var expired = Today.AddDays(-91);
        var kept = Today.AddDays(-90);
        await using (var context = TestDb.NewContext())
        {
            context.PageViews.AddRange(
                View(expired, "/urun/a", "instagram.com", hour: 10, halfHour: false),
                View(expired, "/urun/a", "instagram.com", hour: 10, halfHour: false),
                View(expired, "/urun/a", null, hour: 12, halfHour: true),
                Event(expired, AnalyticsEvent.Order),
                View(kept, "/urun/b", null, hour: 9, halfHour: false),
                View(kept, "/urun/b", null, hour: 9, halfHour: true));
            await context.SaveChangesAsync();
        }

        string before;
        await using (var context = TestDb.NewContext())
        {
            before = JsonSerializer.Serialize(await TestData.NewAnalyticsManager(context).GetReportAsync(expired.AddDays(-5), Today));
        }

        int deleted;
        await using (var context = TestDb.NewContext())
        {
            deleted = await TestData.NewAnalyticsManager(context).RollupAsync(Today);
        }

        await using var check = TestDb.NewContext();
        Assert.Equal(4, deleted);
        Assert.Equal([kept.ToDateTime(TimeOnly.MinValue)], (await check.PageViews.Select(p => p.Day).Distinct().ToListAsync()));
        Assert.Contains(await check.AnalyticsDailies.ToListAsync(), d => d.Day == expired.ToDateTime(TimeOnly.MinValue) && d.Path == "/urun/a" && d.Source == "instagram.com" && d.Views == 2 && d.Visits == 1);
        var after = JsonSerializer.Serialize(await TestData.NewAnalyticsManager(check).GetReportAsync(expired.AddDays(-5), Today));
        Assert.Equal(before, after);
    }

    private static PageView View(DateOnly day, string path, string? referrerHost, byte hour, bool halfHour) => new()
    {
        Event = AnalyticsEvent.View,
        Path = path,
        ReferrerHost = referrerHost,
        Device = "mobil",
        Day = day.ToDateTime(TimeOnly.MinValue),
        Hour = hour,
        HalfHour = halfHour
    };

    private static PageView Event(DateOnly day, string name) => new()
    {
        Event = name,
        Path = "/odeme",
        Device = "mobil",
        Day = day.ToDateTime(TimeOnly.MinValue),
        Hour = 12,
        HalfHour = false
    };
}
