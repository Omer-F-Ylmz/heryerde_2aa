using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using HerYerde.Business.Abstract;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace HerYerde.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class AuthController : Controller
{
    /// <summary>Parolası doğrulanmış tarayıcının kodu girmek için süresi.</summary>
    private static readonly TimeSpan SecondFactorWindow = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan ResetResponseFloor = TimeSpan.FromMilliseconds(400);

    private readonly IAdminAuthService _adminAuthService;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _clock;

    public AuthController(IAdminAuthService adminAuthService, IAdminAuditService auditService, TimeProvider clock)
    {
        _adminAuthService = adminAuthService;
        _auditService = auditService;
        _clock = clock;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (status, result) = await _adminAuthService.SignInAsync(model.Email, model.Password, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        var admin = result.Data!;
        if (admin.TotpEnabled)
        {
            await SignInPendingAsync(admin);
            return Redirect("/admin/auth/iki-adim");
        }

        await SignInAsync(admin);
        if (admin.MustChangePassword)
        {
            return Redirect(AdminPolicy.ChangePasswordPath);
        }

        // Location başlığı yalnız yazdırılabilir ASCII taşır; aksi Kestrel'de 500 olurdu.
        return Url.IsLocalUrl(returnUrl) && returnUrl!.All(c => c > ' ' && c < 127)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Products");
    }

    [HttpGet("admin/auth/iki-adim")]
    public IActionResult SecondFactor() => PendingAdminId() is null ? RedirectToAction(nameof(Login)) : View(new SecondFactorViewModel());

    [HttpPost("admin/auth/iki-adim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SecondFactor(SecondFactorViewModel model, CancellationToken cancellationToken)
    {
        if (PendingAdminId() is not { } adminId)
        {
            return RedirectToAction(nameof(Login));
        }

        var (status, result) = await _adminAuthService.VerifySecondFactorAsync(adminId, model.Code ?? string.Empty, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            // Kilitlendiyse bekleyen çerez de düşer: yeniden parola ister (kilit sürdükçe o da reddedilir).
            if (status == HttpStatusCode.Unauthorized)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new SecondFactorViewModel { ErrorMessage = result.Message });
        }

        await SignInAsync(result.Data!);
        return RedirectToAction("Index", "Products");
    }

    [HttpGet("admin/auth/sifremi-unuttum")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("admin/auth/sifremi-unuttum")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(model);
        }

        // Kayıtlı e-postada anahtar üretilip posta kuyruğa yazılır; asgari süre yanıt süresinden hesabın varlığı okunmasın diye.
        var started = Stopwatch.GetTimestamp();
        var (_, result) = await _adminAuthService.RequestPasswordResetAsync(model.Email.Trim(), cancellationToken);
        if (ResetResponseFloor - Stopwatch.GetElapsedTime(started) is { Ticks: > 0 } remaining)
        {
            await Task.Delay(remaining, cancellationToken);
        }

        return View(new ForgotPasswordViewModel { Sent = result.Message });
    }

    [HttpGet("admin/auth/sifre-sifirla")]
    public IActionResult ResetPassword([FromQuery(Name = "t")] string? token) => View(new ResetPasswordViewModel { Token = token ?? string.Empty });

    [HttpPost("admin/auth/sifre-sifirla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(model);
        }

        var (status, result) = await _adminAuthService.ResetPasswordAsync(model.Token, model.NewPassword, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View(new ResetPasswordViewModel { Token = model.Token, ErrorMessage = result.Message });
        }

        TempData[LoginNoticeKey] = result.Message;
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("admin/sifre")]
    [Authorize(Policy = AdminPolicy.Name)]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel { MustChange = User.HasClaim(c => c.Type == AdminPolicy.MustChangeClaim) });

    [HttpPost("admin/sifre")]
    [Authorize(Policy = AdminPolicy.Name)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(model);
        }

        var (status, result) = await _adminAuthService.ChangePasswordAsync(
            CurrentAdminId,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);

        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View(new ChangePasswordViewModel { ErrorMessage = result.Message });
        }

        // Damga ilerledi: bu tarayıcı yeni damgayla yeniden imzalanır, diğerleri düşer.
        await SignInAsync(result.Data!);
        await _auditService.WriteAsync(HttpContext, "parola değiştir", "yönetici", CurrentAdminId);
        return View(new ChangePasswordViewModel { Changed = true });
    }

    [HttpPost("admin/oturumlar/kapat")]
    [Authorize(Policy = AdminPolicy.Name)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSessions(CancellationToken cancellationToken)
    {
        var (status, result) = await _adminAuthService.RevokeSessionsAsync(CurrentAdminId, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        await SignInAsync(result.Data!);
        await _auditService.WriteAsync(HttpContext, "tüm oturumları kapat", "yönetici", CurrentAdminId);
        return Redirect(AdminPolicy.ChangePasswordPath);
    }

    [HttpGet("admin/iki-adim")]
    [Authorize(Policy = AdminPolicy.Name)]
    public async Task<IActionResult> TwoFactor(CancellationToken cancellationToken)
    {
        var (status, result) = await _adminAuthService.BeginTotpSetupAsync(CurrentAdminId, cancellationToken);
        return status == HttpStatusCode.OK ? View(SetupView(result.Data!)) : NotFound();
    }

    [HttpPost("admin/iki-adim")]
    [Authorize(Policy = AdminPolicy.Name)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactor(SecondFactorViewModel model, CancellationToken cancellationToken)
    {
        var (status, result) = await _adminAuthService.EnableTotpAsync(CurrentAdminId, model.Code ?? string.Empty, cancellationToken);
        var (_, admin) = await _adminAuthService.BeginTotpSetupAsync(CurrentAdminId, cancellationToken);
        var view = SetupView(admin.Data!);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            view.ErrorMessage = result.Message;
            return View(view);
        }

        await _auditService.WriteAsync(HttpContext, "iki adımlı doğrulamayı aç", "yönetici", CurrentAdminId);
        view.RecoveryCodes = result.Data!;
        return View(view);
    }

    [HttpPost("admin/iki-adim/kapat")]
    [Authorize(Policy = AdminPolicy.Name)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableTwoFactor(string? password, CancellationToken cancellationToken)
    {
        var (status, result) = await _adminAuthService.DisableTotpAsync(CurrentAdminId, password ?? string.Empty, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            var (_, admin) = await _adminAuthService.BeginTotpSetupAsync(CurrentAdminId, cancellationToken);
            var view = SetupView(admin.Data!);
            view.ErrorMessage = result.Message;
            Response.StatusCode = (int)status;
            return View(nameof(TwoFactor), view);
        }

        await _auditService.WriteAsync(HttpContext, "iki adımlı doğrulamayı kapat", "yönetici", CurrentAdminId);
        return Redirect("/admin/iki-adim");
    }

    /// <summary>Şifre sıfırlandıktan sonra giriş ekranında gösterilen not.</summary>
    public const string LoginNoticeKey = "giris-notu";

    private int CurrentAdminId
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Bekleyen iki adımlı çerezin yöneticisi; çerez yoksa ya da 5 dakikayı geçtiyse null.</summary>
    private int? PendingAdminId()
        => long.TryParse(User.FindFirst(AdminPolicy.PendingSecondFactorClaim)?.Value, out var issued)
           && _clock.GetUtcNow().UtcDateTime - new DateTime(issued, DateTimeKind.Utc) <= SecondFactorWindow
           && CurrentAdminId > 0
            ? CurrentAdminId
            : null;

    private TwoFactorSetupViewModel SetupView(AdminUser admin)
    {
        var view = new TwoFactorSetupViewModel { Enabled = admin.TotpEnabled };
        if (!admin.TotpEnabled && admin.TotpSecret is { } secret)
        {
            using var data = QRCodeGenerator.GenerateQrCode(Totp.SetupUri("HerYerde", admin.Email, secret), QRCodeGenerator.ECCLevel.M);
            view.Secret = secret;
            view.QrDataUri = "data:image/png;base64," + Convert.ToBase64String(new PngByteQRCode(data).GetGraphic(6));
        }

        return view;
    }

    private Task SignInPendingAsync(AdminUser admin)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(AdminPolicy.PendingSecondFactorClaim, _clock.GetUtcNow().UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture))
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private Task SignInAsync(AdminUser admin)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, admin.Email),
            new Claim(AdminPolicy.ClaimType, AdminPolicy.ClaimValue),
            new Claim(AdminPolicy.StampClaim, admin.PasswordChangedAt.Ticks.ToString(CultureInfo.InvariantCulture))
        ];
        if (admin.MustChangePassword)
        {
            claims.Add(new Claim(AdminPolicy.MustChangeClaim, "true"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
