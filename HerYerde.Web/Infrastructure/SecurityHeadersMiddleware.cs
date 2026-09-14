using HerYerde.Business;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Infrastructure;

/// <summary>Her yanıta güvenlik başlıklarını koyar. Sayfalarda satır içi script/style yok, bu yüzden
/// CSP'de nonce'a gerek kalmıyor; yazı tipleri de kendi sunucumuzdan geldiği için font-src yalnız 'self'.
/// Başlıklar yanıt başlarken yazılır: hata sayfası (UseExceptionHandler yanıtı temizler) da onları taşır.</summary>
public sealed class SecurityHeadersMiddleware
{
    private const string PermissionsPolicy = "camera=(), microphone=(), geolocation=()";

    private readonly RequestDelegate _next;
    private readonly string _contentSecurityPolicy;
    private readonly string _checkoutContentSecurityPolicy;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<ShopSettings> shop, IOptions<IyzicoSettings> iyzico)
    {
        _next = next;
        // Dış görsel yalnız Shop:ImageOrigins'teki kökenlerden; "https:" gibi şema-joker kaynak yok.
        var imageSources = string.Join(' ', new[] { "'self'", "data:" }.Concat(shop.Value.ImageOrigins));
        _contentSecurityPolicy =
            "default-src 'self'; " +
            $"img-src {imageSources}; " +
            "script-src 'self'; " +
            "style-src 'self'; " +
            "font-src 'self'; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'";

        // 3D doğrulama formu bankaya/İyzico'ya gönderilir: yalnız ödeme sayfalarında o kökenlere izin verilir.
        var paymentSources = string.Join(' ', iyzico.Value.CspSources);
        _checkoutContentSecurityPolicy = paymentSources.Length == 0
            ? _contentSecurityPolicy
            : _contentSecurityPolicy + $" {paymentSources}; frame-src {paymentSources}";
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["Content-Security-Policy"] = context.Request.Path.StartsWithSegments("/odeme")
                ? _checkoutContentSecurityPolicy
                : _contentSecurityPolicy;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            return Task.CompletedTask;
        });
        return _next(context);
    }
}
