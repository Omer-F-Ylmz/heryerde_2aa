namespace HerYerde.Web.Infrastructure;

/// <summary>Son siparişin erişim anahtarı; teşekkür sayfası bağlantıdaki ?t olmadan da bununla açılır.</summary>
public static class LastOrderCookie
{
    public const string Name = "heryerde.lastorder";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static Guid? Read(HttpContext context)
        => context.Request.Cookies.TryGetValue(Name, out var raw) && Guid.TryParse(raw, out var token) ? token : null;

    public static void Write(HttpContext context, Guid token)
        => context.Response.Cookies.Append(Name, token.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.Add(Lifetime)
        });
}
