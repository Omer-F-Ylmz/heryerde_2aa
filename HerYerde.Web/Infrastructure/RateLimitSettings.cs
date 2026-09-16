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

    /// <summary>Video yükleme yeniden kodlama demek: görselden çok daha pahalı, ayrı ve dar kova.</summary>
    public int VideoUploadPerMinute { get; set; } = 5;

    /// <summary>İletişim formu gönderimi.</summary>
    public int ContactPerMinute { get; set; } = 5;

    /// <summary>Ürün yorumu gönderimi; günlük pencere.</summary>
    public int ReviewPerDay { get; set; } = 3;

    /// <summary>Sipariş sorgulama: numara + telefon denemesi; tahmin yoluyla sipariş bulmayı yavaşlatır.</summary>
    public int OrderLookupPerMinute { get; set; } = 10;

    /// <summary>Havale bildirimi (dekont yüklemesi olabilir).</summary>
    public int PaymentNoticePerMinute { get; set; } = 5;

    public int GeneralPerMinute { get; set; } = 300;
}

public static class RateLimitPolicy
{
    /// <summary>Yol ve yönteme göre tek bir bölüm seçer: yönetici girişi, görsel yükleme, sepet, 3D dönüşü, ödeme, iletişim, yorum ya da genel.
    /// Hata sayfası yeniden çalıştırıldığında sınır uygulanmaz, yoksa 429 sayfası da 429 olurdu. Sağlık uçları da
    /// sınırsızdır: izleme servisi sık yoklar, 429 kesinti gibi görünür.</summary>
    public static RateLimitPartition<string> Select(HttpContext context)
    {
        if (context.Features.Get<IStatusCodeReExecuteFeature>() is not null)
        {
            return RateLimitPartition.GetNoLimiter("yeniden-calistirma");
        }

        if (context.Request.Path.StartsWithSegments("/health"))
        {
            return RateLimitPartition.GetNoLimiter("saglik");
        }

        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen";
        var path = context.Request.Path;
        var isPost = HttpMethods.IsPost(context.Request.Method);

        // Parola sıfırlama ve iki adımlı kod girişle aynı kovada: tahmin denemeleri toplamda sınırlanır.
        var (bucket, permit) = path.StartsWithSegments("/admin/auth/login")
                               || isPost && (path.StartsWithSegments("/admin/auth/iki-adim")
                                             || path.StartsWithSegments("/admin/auth/sifremi-unuttum")
                                             || path.StartsWithSegments("/admin/auth/sifre-sifirla"))
            ? ("admin-giris", settings.AdminLoginPerMinute)
            : isPost && path.StartsWithSegments("/admin/products/addvideo")
                ? ("video", settings.VideoUploadPerMinute)
                : isPost && (path.StartsWithSegments("/admin/products/addimage") || path.StartsWithSegments("/admin/products/import"))
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
                                    : isPost && path.StartsWithSegments("/siparis-sorgula")
                                        ? ("siparis-sorgula", settings.OrderLookupPerMinute)
                                        : isPost && path.StartsWithSegments("/ceyizlistesi/yeni")
                                        ? ("ceyiz-olustur", settings.ContactPerMinute)
                                        : isPost && path.StartsWithSegments("/siparis") && IsOrderActionPost(path)
                                            ? ("odeme-bildir", settings.PaymentNoticePerMinute)
                                            : ("genel", settings.GeneralPerMinute);

        return RateLimitPartition.GetFixedWindowLimiter($"{bucket}:{client}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = bucket == "yorum" ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    }

    /// <summary>Müşterinin sipariş sayfasından yaptığı işlemler (havale bildirimi, iade talebi, iptal) aynı kovada.</summary>
    private static bool IsOrderActionPost(PathString path)
        => path.Value!.EndsWith("/odeme-bildir", StringComparison.OrdinalIgnoreCase)
           || path.Value.EndsWith("/iade", StringComparison.OrdinalIgnoreCase)
           || path.Value.EndsWith("/iptal", StringComparison.OrdinalIgnoreCase);

    /// <summary>/urun/{slug}/yorum</summary>
    private static bool IsReviewPost(PathString path)
        => path.StartsWithSegments("/urun") && path.Value!.EndsWith("/yorum", StringComparison.OrdinalIgnoreCase);
}
