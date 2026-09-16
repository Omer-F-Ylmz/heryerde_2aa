using HerYerde.Business.Rules;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Tıklama olayları için beacon ucu (navigator.sendBeacon): çerez ve antiforgery anahtarı taşımaz, durum değiştirmez;
/// yalnız izinli tıklama olayı (WhatsApp, Instagram) yazılır, huni adımları buradan kabul edilmez.</summary>
public class EventController(AnalyticsRecorder analytics) : Controller
{
    [HttpPost("olay")]
    [IgnoreAntiforgeryToken]
    public IActionResult Record([FromForm] string? ad, [FromForm] string? yol)
    {
        if (ad is null || !AnalyticsEvent.ClientEvents.Contains(ad))
        {
            return BadRequest();
        }

        var path = yol is { Length: > 0 } && yol.StartsWith('/') && !yol.StartsWith("//", StringComparison.Ordinal)
            ? yol.Split('?', '#')[0]
            : "/";
        analytics.Record(HttpContext, ad, path);
        return NoContent();
    }
}
