using HerYerde.Business.Rules;

namespace HerYerde.Web.Infrastructure;

/// <summary>Başarılı (200) HTML GET yanıtını sayfa görüntüleme olarak kaydeder; yönetim, sağlık, akış, sipariş ve benzeri yollar
/// AnalyticsRules'ta dışarıda. Statik dosyalar bu ara katmana gelmez.</summary>
public sealed class PageViewMiddleware(RequestDelegate next, AnalyticsRecorder recorder)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (HttpMethods.IsGet(context.Request.Method)
            && context.Response.StatusCode == StatusCodes.Status200OK
            && context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true
            && AnalyticsRules.IsTrackedPath(context.Request.Path.Value ?? "/"))
        {
            recorder.Record(context, AnalyticsEvent.View);
        }
    }
}
