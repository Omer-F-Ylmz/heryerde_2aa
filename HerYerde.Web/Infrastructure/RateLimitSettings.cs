using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Infrastructure;

/// <summary>Dakikalık istek üst sınırları; hepsi IP başına sayılır.</summary>
public sealed class RateLimitSettings
{
    public int AdminLoginPerMinute { get; set; } = 10;

    public int CartPerMinute { get; set; } = 60;

    public int CheckoutPerMinute { get; set; } = 5;

    public int GeneralPerMinute { get; set; } = 300;
}

public static class RateLimitPolicy
{
    /// <summary>Yol ve yönteme göre tek bir bölüm seçer: yönetici girişi, sepet, ödeme ya da genel.
    /// Hata sayfası yeniden çalıştırıldığında sınır uygulanmaz, yoksa 429 sayfası da 429 olurdu.</summary>
    public static RateLimitPartition<string> Select(HttpContext context)
    {
        if (context.Features.Get<IStatusCodeReExecuteFeature>() is not null)
        {
            return RateLimitPartition.GetNoLimiter("yeniden-calistirma");
        }

        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen";
        var path = context.Request.Path;
        var isPost = HttpMethods.IsPost(context.Request.Method);

        var (bucket, permit) = path.StartsWithSegments("/admin/auth/login")
            ? ("admin-giris", settings.AdminLoginPerMinute)
            : isPost && path.StartsWithSegments("/sepet")
                ? ("sepet", settings.CartPerMinute)
                : isPost && path.StartsWithSegments("/odeme")
                    ? ("odeme", settings.CheckoutPerMinute)
                    : ("genel", settings.GeneralPerMinute);

        return RateLimitPartition.GetFixedWindowLimiter($"{bucket}:{client}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    }
}
