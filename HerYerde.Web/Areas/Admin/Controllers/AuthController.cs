using System.Globalization;
using System.Net;
using System.Security.Claims;
using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class AuthController : Controller
{
    private readonly IAdminAuthService _adminAuthService;
    private readonly IAdminAuditService _auditService;

    public AuthController(IAdminAuthService adminAuthService, IAdminAuditService auditService)
    {
        _adminAuthService = adminAuthService;
        _auditService = auditService;
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

        await SignInAsync(result.Data!);

        return Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl!)
            : RedirectToAction("Index", "Products");
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
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

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

    private int CurrentAdminId
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private Task SignInAsync(AdminUser admin)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, admin.Email),
                new Claim(AdminPolicy.ClaimType, AdminPolicy.ClaimValue),
                new Claim(AdminPolicy.StampClaim, admin.PasswordChangedAt.Ticks.ToString(CultureInfo.InvariantCulture))
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
