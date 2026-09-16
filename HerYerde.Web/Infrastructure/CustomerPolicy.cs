using System.Globalization;
using System.Security.Claims;
using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HerYerde.Web.Infrastructure;

/// <summary>Üye oturumu: yönetim çerezinden ayrı şema ve çerez (heryerde.customer). /admin dışındaki isteklerde varsayılan
/// kimlik bu şemadan okunur; üye kimliği NameIdentifier'da değil kendi talebinde durur (denetim izi yöneticiyle karışmaz).</summary>
public static class CustomerPolicy
{
    public const string Scheme = "Customer";
    public const string Name = "Customer";

    /// <summary>İsteğin yoluna göre yönetim ya da üye şemasına yönlendiren varsayılan şema.</summary>
    public const string SelectorScheme = "HerYerde";

    public const string CookieName = "heryerde.customer";
    public const string IdClaim = "heryerde:customer";
    public const string StampClaim = "heryerde:customer-stamp";
    public const string LoginPath = "/hesap/giris";
    public const string ReturnParameter = "donus";

    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public static int? CustomerId(ClaimsPrincipal user)
        => int.TryParse(user.FindFirst(IdClaim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    public static string SelectScheme(HttpContext context)
        => context.Request.Path.StartsWithSegments("/admin") ? CookieAuthenticationDefaults.AuthenticationScheme : Scheme;

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = LoginPath;
        options.AccessDeniedPath = LoginPath;
        options.ReturnUrlParameter = ReturnParameter;
        // 30 gün, kullanıldıkça uzar. Süre gerçek saatle sayılır: çerezin tarayıcıdaki ömrü de gerçek saate bağlı.
        options.ExpireTimeSpan = SessionLifetime;
        options.SlidingExpiration = true;
        options.TimeProvider = TimeProvider.System;
        options.Events.OnValidatePrincipal = ValidateStampAsync;
    }

    /// <summary>Kalıcı çerez: tarayıcı kapanınca oturum düşmez, 30 gün kullanılmazsa düşer.</summary>
    public static Task SignInAsync(HttpContext context, Customer customer)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(IdClaim, customer.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(StampClaim, customer.SessionStamp.Ticks.ToString(CultureInfo.InvariantCulture))
            ],
            Scheme);
        return context.SignInAsync(Scheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
    }

    public static Task SignOutAsync(HttpContext context) => context.SignOutAsync(Scheme);

    /// <summary>Oturum damgası veritabanındakiyle uymazsa (parola değişti, "tüm cihazlardan çık", hesap silindi) çerez düşer.</summary>
    public static async Task ValidateStampAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var id = principal is null ? null : CustomerId(principal);
        var stamp = long.TryParse(principal?.FindFirst(StampClaim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ? ticks : -1;
        var customers = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();

        if (id is null || !await customers.StampIsCurrentAsync(id.Value, stamp, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
        }
    }

    /// <summary>Yerel dönüş adresi; Location başlığı yalnız yazdırılabilir ASCII taşır.</summary>
    public static string SafeReturn(string? returnUrl, string fallback)
        => returnUrl is { Length: > 0 } url && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal)
           && !url.StartsWith("/\\", StringComparison.Ordinal) && url.All(c => c > ' ' && c < 127)
            ? url
            : fallback;
}
