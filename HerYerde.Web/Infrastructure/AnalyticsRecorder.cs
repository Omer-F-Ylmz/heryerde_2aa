using System.Collections.Concurrent;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Models;

namespace HerYerde.Web.Infrastructure;

/// <summary>Çerezsiz analitik kaydını bellekte biriktirir (D16); AnalyticsWriter 30 sn'de bir tek toplu yazımla boşaltır.
/// İstekten yalnız yol, yönlendiren alan adı, UTM ve tarayıcı kimliğinden türeyen cihaz sınıfı alınır; IP ve kimliğin kendisi
/// hiçbir yere yazılmaz. Bot ve boş kimlik sayılmaz.</summary>
public sealed class AnalyticsRecorder(TimeProvider clock)
{
    /// <summary>Yazım uzun süre başarısız kalırsa bellek şişmesin: bu kadar bekleyen kayıttan sonrası atılır.</summary>
    public const int MaxPending = 10_000;

    private readonly ConcurrentQueue<PageView> _pending = new();

    public void Record(HttpContext context, string eventName, string? path = null)
    {
        var request = context.Request;
        var userAgent = request.Headers.UserAgent.ToString();
        if (AnalyticsRules.IsBot(userAgent) || _pending.Count >= MaxPending)
        {
            return;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, IstanbulTime.Zone);
        var target = path ?? request.Path.Value ?? "/";
        _pending.Enqueue(new PageView
        {
            Event = eventName,
            Path = target.Length > AnalyticsRules.PathLength ? target[..AnalyticsRules.PathLength] : target,
            ReferrerHost = AnalyticsRules.ReferrerHost(request.Headers.Referer.ToString(), request.Host.Host),
            UtmSource = AnalyticsRules.Utm(request.Query["utm_source"]),
            UtmMedium = AnalyticsRules.Utm(request.Query["utm_medium"]),
            UtmCampaign = AnalyticsRules.Utm(request.Query["utm_campaign"]),
            Device = AnalyticsRules.DeviceOf(userAgent),
            Day = local.Date,
            Hour = (byte)local.Hour,
            HalfHour = local.Minute >= 30
        });
    }

    public List<PageView> Drain()
    {
        var batch = new List<PageView>();
        while (_pending.TryDequeue(out var view))
        {
            batch.Add(view);
        }

        return batch;
    }
}
