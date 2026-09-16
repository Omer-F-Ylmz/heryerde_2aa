using System.Globalization;
using HerYerde.Business.Dtos;

namespace HerYerde.Web.Areas.Admin.Models;

/// <summary>Analitik sayfası: rapor, yol → ad eşlemesi ve grafiğin boşluksuz gün dizisi.</summary>
public sealed record AnalyticsPageViewModel(AnalyticsReport Report, IReadOnlyDictionary<string, string> Names)
{
    /// <summary>Günlük grafik çubuğunun en yüksek boyu (SVG birimi).</summary>
    public const int ChartHeight = 120;

    public string NameOf(string path) => Names.TryGetValue(path, out var name) ? name : path;

    public static string SourceLabel(string source) => source.Length == 0 ? "Doğrudan / site içi" : source;

    public static string DeviceLabel(string device) => device switch
    {
        "mobil" => "Mobil",
        "tablet" => "Tablet",
        _ => "Masaüstü"
    };

    /// <summary>Aralıktaki her gün (kaydı olmayan gün 0).</summary>
    public IReadOnlyList<AnalyticsDayRow> AllDays
    {
        get
        {
            var byDay = Report.Days.ToDictionary(d => d.Day);
            return Enumerable.Range(0, Report.To.DayNumber - Report.From.DayNumber + 1)
                .Select(offset => Report.From.AddDays(offset))
                .Select(day => byDay.TryGetValue(day, out var row) ? row : new AnalyticsDayRow(day, 0, 0))
                .ToList();
        }
    }

    public int BarHeight(int value, int max) => max == 0 ? 0 : (int)Math.Round(value * (double)ChartHeight / max);

    /// <summary>Önceki adıma oran; önceki adım 0 ise tire.</summary>
    public static string Rate(int value, int previous)
        => previous == 0 ? "–" : "%" + (value * 100.0 / previous).ToString("0.#", CultureInfo.GetCultureInfo("tr-TR"));
}
