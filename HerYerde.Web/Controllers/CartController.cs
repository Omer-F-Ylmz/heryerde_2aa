using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Anonim sepet: kimlik "heryerde.cart" çerezinde, satır işlemleri POST + yönlendirme.</summary>
public class CartController(ICartService cartService) : Controller
{
    public const string ErrorKey = "SepetHatasi";

    [HttpGet("sepet")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cartId = CartCookie.Read(HttpContext);
        if (cartId is null)
        {
            return View(new CartPageViewModel(new CartView(Guid.Empty, [], 0m, 0m), TempData[ErrorKey] as string));
        }

        var (_, view) = await cartService.GetAsync(cartId.Value, cancellationToken);
        return View(new CartPageViewModel(view.Data!, TempData[ErrorKey] as string));
    }

    [HttpPost("sepet/ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int? variantId, int quantity, string? donus, CancellationToken cancellationToken)
    {
        var (_, cart) = await cartService.GetOrCreateAsync(CartCookie.Read(HttpContext), cancellationToken);
        CartCookie.Write(HttpContext, cart.Data!.Id);

        var (status, result) = await cartService.AddAsync(cart.Data.Id, productId, variantId, quantity, cancellationToken);
        if (status != System.Net.HttpStatusCode.OK)
        {
            return await RejectedAsync(cart.Data.Id, status, result.Message, cancellationToken);
        }

        return Redirect(LocalOr(donus, "/sepet"));
    }

    [HttpPost("sepet/guncelle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetQuantity(int itemId, int quantity, CancellationToken cancellationToken)
    {
        if (CartCookie.Read(HttpContext) is not { } cartId)
        {
            return Redirect("/sepet");
        }

        var (status, result) = await cartService.SetQuantityAsync(cartId, itemId, quantity, cancellationToken);
        if (status != System.Net.HttpStatusCode.OK)
        {
            return await RejectedAsync(cartId, status, result.Message, cancellationToken);
        }

        return Redirect("/sepet");
    }

    [HttpPost("sepet/sil")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Remove(int itemId, CancellationToken cancellationToken)
        => SetQuantity(itemId, 0, cancellationToken);

    /// <summary>Geçersiz sepet isteği sessizce yuvarlanmaz: durum kodu korunur, sepet sayfası mesajla çizilir.</summary>
    private async Task<IActionResult> RejectedAsync(
        Guid cartId,
        System.Net.HttpStatusCode status,
        string message,
        CancellationToken cancellationToken)
    {
        var (_, view) = await cartService.GetAsync(cartId, cancellationToken);
        Response.StatusCode = (int)status;
        return View("Index", new CartPageViewModel(view.Data!, message));
    }

    /// <summary>Açık yönlendirmeyi engeller: yalnız site içi adrese döner.</summary>
    private string LocalOr(string? url, string fallback)
        => !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url) ? url : fallback;
}
