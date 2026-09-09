namespace HerYerde.Web.Infrastructure;

/// <summary>Anonim sepetin kimliğini taşıyan çerez; hesap yok, sepet 30 gün bu çerezle bulunur.</summary>
public static class CartCookie
{
    public const string Name = "heryerde.cart";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static Guid? Read(HttpContext context)
        => context.Request.Cookies.TryGetValue(Name, out var raw) && Guid.TryParse(raw, out var id) ? id : null;

    public static void Write(HttpContext context, Guid cartId)
        => context.Response.Cookies.Append(Name, cartId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.Add(Lifetime)
        });
}
