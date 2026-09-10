using System.Net;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Entities.Enums;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Controllers;

/// <summary>Ödeme adımı ve teşekkür sayfası. Kart/sanal POS yok; kapıda ödeme veya havale.</summary>
public class CheckoutController(
    ICartService cartService,
    IOrderService orderService,
    IOptions<ShopSettings> shop,
    IConfiguration configuration) : Controller
{
    private ShopSettings Shop => shop.Value;

    /// <summary>Sepetteki fiyat donmasın diye ödeme adımına girerken satırlar yeniden değerlenir.</summary>
    private const string PriceChangedMessage =
        "Sepetinizdeki bazı ürünlerin fiyatı güncellendi. Yeni tutarı onaylayıp devam edebilirsiniz.";

    [HttpGet("odeme")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (CartCookie.Read(HttpContext) is not { } cartId)
        {
            return Redirect("/sepet");
        }

        var revalued = await cartService.RevalueAsync(cartId, cancellationToken);
        var cart = await CurrentCartAsync(cancellationToken);
        if (cart is null || cart.IsEmpty)
        {
            return Redirect("/sepet");
        }

        return View(new CheckoutPageViewModel(
            new CheckoutFormViewModel(),
            cart,
            Shop.Iban,
            null,
            revalued > 0 ? PriceChangedMessage : null));
    }

    [HttpPost("odeme")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Place(CheckoutFormViewModel form, CancellationToken cancellationToken)
    {
        var cartId = CartCookie.Read(HttpContext);
        var cart = await CurrentCartAsync(cancellationToken);
        if (cartId is null || cart is null || cart.IsEmpty)
        {
            return Redirect("/sepet");
        }

        // Fiyat değiştiyse sipariş açılmaz: kullanıcı yeni tutarı sepette görüp yeniden onaylar.
        if (await cartService.RevalueAsync(cartId.Value, cancellationToken) > 0)
        {
            TempData[CartController.ErrorKey] = PriceChangedMessage;
            Response.Headers.Location = "/sepet";
            return StatusCode(StatusCodes.Status303SeeOther);
        }

        if (form.PaymentMethod is not (PaymentMethod.KapidaOdeme or PaymentMethod.HavaleEft))
        {
            ModelState.AddModelError(nameof(form.PaymentMethod), "Şimdilik kapıda ödeme ve havale/EFT var.");
        }

        if (!ModelState.IsValid)
        {
            return Invalid(form, cart, null);
        }

        var (status, result) = await orderService.PlaceAsync(cartId.Value, new OrderDraft(
            form.FullName,
            form.Phone,
            form.Email,
            form.Address,
            form.City,
            form.District,
            form.Note,
            form.PaymentMethod), cancellationToken);

        if (status == HttpStatusCode.Created)
        {
            var placed = result.Data!;
            LastOrderCookie.Write(HttpContext, placed.AccessToken);
            return Redirect($"/siparis/{placed.OrderNo}/tesekkur?t={placed.AccessToken}");
        }

        // Stok yetersizse satırlar sepette işaretli görünsün diye güncel sepetle dönüyoruz.
        var refreshed = await CurrentCartAsync(cancellationToken) ?? cart;
        Response.StatusCode = (int)status;
        return Invalid(form, refreshed, result.Message, setStatus: false);
    }

    /// <summary>Sipariş numarası tahmin edilebilir; sayfa yalnız ?t anahtarıyla veya son sipariş çereziyle açılır.</summary>
    [HttpGet("siparis/{orderNo}/tesekkur")]
    public async Task<IActionResult> ThankYou(string orderNo, [FromQuery(Name = "t")] string? t, CancellationToken cancellationToken)
    {
        var (status, result) = await orderService.GetByOrderNoAsync(orderNo, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        var token = result.Data!.Order.AccessToken;
        if (!(Guid.TryParse(t, out var supplied) && supplied == token) && LastOrderCookie.Read(HttpContext) != token)
        {
            return NotFound();
        }

        var whatsAppBase = configuration["Shop:WhatsApp"] ?? "https://wa.me/";
        var message = $"Merhaba, {result.Data.Order.OrderNo} numaralı siparişimi bildirmek istiyorum.";
        return View(new ThankYouViewModel(result.Data, whatsAppBase + "?text=" + Uri.EscapeDataString(message), Shop.Iban));
    }

    private IActionResult Invalid(CheckoutFormViewModel form, CartView cart, string? message, bool setStatus = true)
    {
        if (setStatus)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        return View("Index", new CheckoutPageViewModel(form, cart, Shop.Iban, message));
    }

    private async Task<CartView?> CurrentCartAsync(CancellationToken cancellationToken)
    {
        if (CartCookie.Read(HttpContext) is not { } cartId)
        {
            return null;
        }

        var (_, view) = await cartService.GetAsync(cartId, cancellationToken);
        return view.Data;
    }
}
