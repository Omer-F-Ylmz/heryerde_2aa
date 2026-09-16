using System.Security.Cryptography;
using System.Text;

namespace HerYerde.Web.Infrastructure;

/// <summary>Staging kapısı: her yanıta noindex başlığı; sağlık uçları dışındaki her istek Basic Auth ister (StagingGate:User/Password,
/// CD'de STAGING_USER/STAGING_PASS). Kimlik ayarlanmamışsa kapı kapalı kalır: yanlış yapılandırılmış staging dışarı açılmaz.</summary>
public sealed class StagingGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[]? _expected;

    public StagingGateMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var user = configuration["StagingGate:User"];
        var password = configuration["StagingGate:Password"];
        _expected = string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password) ? null : Digest(user + ":" + password);
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            return Task.CompletedTask;
        });

        // Konteyner sağlık denetimi ve çalışma izleme kimliksiz yoklar; bu uçlar içerik göstermez.
        if (context.Request.Path.StartsWithSegments("/health") || Authorized(context.Request))
        {
            return _next(context);
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"HerYerde staging\", charset=\"UTF-8\"";
        return Task.CompletedTask;
    }

    private bool Authorized(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (_expected is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var supplied = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            // Özetler sabit uzunlukta: karşılaştırma süresi parolanın uzunluğunu ya da ortak önekini ele vermez.
            return CryptographicOperations.FixedTimeEquals(Digest(supplied), _expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Digest(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
