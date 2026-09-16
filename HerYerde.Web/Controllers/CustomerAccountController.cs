using System.Diagnostics;
using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Üyelik: kayıt, e-posta doğrulama, parolalı ve parolasız giriş, parola sıfırlama, çıkış. Hesabın varlığına bağlı
/// yanıtlar (kayıt, giriş bağlantısı, sıfırlama) aynı ileti ve asgari süreyle döner.</summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class CustomerAccountController(ICustomerService customers) : Controller
{
    /// <summary>Posta yazılsın yazılmasın yanıt en az bu sürede döner.</summary>
    public static readonly TimeSpan ResponseFloor = TimeSpan.FromMilliseconds(400);

    [HttpGet("hesap/kayit")]
    public IActionResult SignUp()
        => CustomerPolicy.CustomerId(User) is null ? View(new SignUpViewModel()) : Redirect("/hesap");

    [HttpPost("hesap/kayit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignUp(SignUpViewModel model, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var (status, result) = await customers.SignUpAsync(
            new CustomerSignUp(model.Email ?? string.Empty, model.Password, model.KvkkConsent, model.MarketingConsent),
            cancellationToken);
        await FloorAsync(started, cancellationToken);

        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            model.Password = null;
            model.ErrorMessage = result.Message;
            return View(model);
        }

        return View(new SignUpViewModel { Sent = result.Message });
    }

    [HttpGet("hesap/dogrula")]
    public async Task<IActionResult> Verify([FromQuery(Name = "t")] string? token, CancellationToken cancellationToken)
    {
        var customer = await customers.PeekVerifyAsync(token ?? string.Empty, cancellationToken);
        if (customer is null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        return View("Confirm", new LinkConfirmViewModel
        {
            Token = token ?? string.Empty,
            Verify = true,
            AskPassword = customer?.PasswordHash is not null,
            Invalid = customer is null
        });
    }

    [HttpPost("hesap/dogrula")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(LinkConfirmViewModel model, CancellationToken cancellationToken)
    {
        var (status, result) = await customers.VerifyEmailAsync(model.Token, model.Password, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            var customer = await customers.PeekVerifyAsync(model.Token, cancellationToken);
            return View("Confirm", new LinkConfirmViewModel
            {
                Token = model.Token,
                Verify = true,
                AskPassword = customer?.PasswordHash is not null,
                Invalid = customer is null,
                ErrorMessage = result.Message
            });
        }

        await CustomerPolicy.SignInAsync(HttpContext, result.Data!);
        return Redirect("/hesap");
    }

    [HttpGet("hesap/giris")]
    public IActionResult Login([FromQuery(Name = CustomerPolicy.ReturnParameter)] string? returnUrl)
        => CustomerPolicy.CustomerId(User) is null
            ? View(new CustomerLoginViewModel { ReturnUrl = returnUrl })
            : Redirect(CustomerPolicy.SafeReturn(returnUrl, "/hesap"));

    [HttpPost("hesap/giris")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        CustomerLoginViewModel model,
        [FromQuery(Name = CustomerPolicy.ReturnParameter)] string? returnUrl,
        CancellationToken cancellationToken)
    {
        model.ReturnUrl ??= returnUrl;
        var (status, result) = await customers.SignInAsync(model.Email ?? string.Empty, model.Password ?? string.Empty, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return View(new CustomerLoginViewModel { Email = model.Email ?? string.Empty, ReturnUrl = model.ReturnUrl, ErrorMessage = result.Message });
        }

        await CustomerPolicy.SignInAsync(HttpContext, result.Data!);
        return Redirect(CustomerPolicy.SafeReturn(model.ReturnUrl, "/hesap"));
    }

    [HttpPost("hesap/giris-baglantisi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestLink(CustomerLoginViewModel model, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var (_, result) = await customers.RequestLoginLinkAsync(model.Email ?? string.Empty, cancellationToken);
        await FloorAsync(started, cancellationToken);
        return View("Login", new CustomerLoginViewModel { Email = model.Email ?? string.Empty, ReturnUrl = model.ReturnUrl, Notice = result.Message });
    }

    [HttpGet("hesap/baglanti")]
    public IActionResult Link([FromQuery(Name = "t")] string? token)
        => View("Confirm", new LinkConfirmViewModel { Token = token ?? string.Empty, Invalid = string.IsNullOrEmpty(token) });

    [HttpPost("hesap/baglanti")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Link(LinkConfirmViewModel model, CancellationToken cancellationToken)
    {
        var (status, result) = await customers.SignInWithLinkAsync(model.Token, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View("Confirm", new LinkConfirmViewModel { Invalid = true, ErrorMessage = result.Message });
        }

        await CustomerPolicy.SignInAsync(HttpContext, result.Data!);
        return Redirect("/hesap");
    }

    [HttpGet("hesap/sifremi-unuttum")]
    public IActionResult ForgotPassword() => View(new CustomerForgotViewModel());

    [HttpPost("hesap/sifremi-unuttum")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(CustomerForgotViewModel model, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var (_, result) = await customers.RequestPasswordResetAsync(model.Email ?? string.Empty, cancellationToken);
        await FloorAsync(started, cancellationToken);
        return View(new CustomerForgotViewModel { Sent = result.Message });
    }

    [HttpGet("hesap/sifre-sifirla")]
    public IActionResult ResetPassword([FromQuery(Name = "t")] string? token)
        => View(new CustomerResetViewModel { Token = token ?? string.Empty });

    [HttpPost("hesap/sifre-sifirla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(CustomerResetViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new CustomerResetViewModel { Token = model.Token, ErrorMessage = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });
        }

        var (status, result) = await customers.ResetPasswordAsync(model.Token, model.NewPassword, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View(new CustomerResetViewModel { Token = model.Token, ErrorMessage = result.Message });
        }

        // Bu tarayıcıdaki eski oturum da damgayla düşer; yeni parolayla giriş istenir.
        await CustomerPolicy.SignOutAsync(HttpContext);
        TempData[LoginNoticeKey] = result.Message;
        return Redirect(CustomerPolicy.LoginPath);
    }

    [HttpPost("hesap/cikis")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await CustomerPolicy.SignOutAsync(HttpContext);
        return Redirect("/");
    }

    public const string LoginNoticeKey = "hesap-giris-notu";

    private static async Task FloorAsync(long started, CancellationToken cancellationToken)
    {
        if (ResponseFloor - Stopwatch.GetElapsedTime(started) is { Ticks: > 0 } remaining)
        {
            await Task.Delay(remaining, cancellationToken);
        }
    }
}
