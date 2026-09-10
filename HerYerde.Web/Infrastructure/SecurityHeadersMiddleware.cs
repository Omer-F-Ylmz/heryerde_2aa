namespace HerYerde.Web.Infrastructure;

/// <summary>Her yanıta güvenlik başlıklarını koyar. Sayfalarda satır içi script/style yok, bu yüzden
/// CSP'de nonce'a gerek kalmıyor; yazı tipleri Google Fonts'tan geldiği için yalnız o kaynak açık.</summary>
public sealed class SecurityHeadersMiddleware
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "img-src 'self' https: data:; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "font-src 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'";

    private const string PermissionsPolicy = "camera=(), microphone=(), geolocation=()";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = PermissionsPolicy;
        return _next(context);
    }
}
