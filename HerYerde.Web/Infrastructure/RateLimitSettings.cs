using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Infrastructure;

/// <summary>İstek üst sınırları; hepsi IP başına sayılır. Yorum dışındakiler dakikalık.</summary>
public sealed class RateLimitSettings
{
    public int AdminLoginPerMinute { get; set; } = 10;

    public int CartPerMinute { get; set; } = 60;

    public int CheckoutPerMinute { get; set; } = 5;

    /// <summary>3D dönüşü: antiforgery taşımadığı için ayrı ve sınırlı.</summary>
    public int ThreeDsCallbackPerMinute { get; set; } = 30;

    /// <summary>Görsel yükleme sunucuda işleme (yeniden boyutlama) demek; ayrı ve dar tutulur.</summary>
    public int UploadPerMinute { get; set; } = 20;

    /// <summary>İletişim formu gönderimi.</summary>
    public int ContactPerMinute { get; set; } = 5;

    /// <summary>Ürün yorumu gönderimi; günlük pencere.</summary>
    public int ReviewPerDay { get; set; } = 3;

    public int GeneralPerMinute { get; set; } = 300;
}

public static class RateLimitPolicy
{
    /// <summary>Yol ve yönteme göre tek bir bölüm seçer: yönetici girişi, görsel yükleme, sepet, 3D dönüşü, ödeme, iletişim, yorum ya da genel.
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
            : isPost && path.StartsWithSegments("/admin/products/addimage")
                ? ("yukleme", settings.UploadPerMinute)
                : isPost && path.StartsWithSegments("/sepet")
                    ? ("sepet", settings.CartPerMinute)
                    : isPost && path.StartsWithSegments("/odeme/3d-donus")
                        ? ("3d-donus", settings.ThreeDsCallbackPerMinute)
                        : isPost && path.StartsWithSegments("/odeme")
                            ? ("odeme", settings.CheckoutPerMinute)
                            : isPost && path.StartsWithSegments("/iletisim")
                                ? ("iletisim", settings.ContactPerMinute)
                                : isPost && IsReviewPost(path)
                                    ? ("yorum", settings.ReviewPerDay)
                                    : ("genel", settings.GeneralPerMinute);

        return RateLimitPartition.GetFixedWindowLimiter($"{bucket}:{client}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = bucket == "yorum" ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    }

    /// <summary>/urun/{slug}/yorum</summary>
    private static bool IsReviewPost(PathString path)
        => path.StartsWithSegments("/urun") && path.Value!.EndsWith("/yorum", StringComparison.OrdinalIgnoreCase);
}
