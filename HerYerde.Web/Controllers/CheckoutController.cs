using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Entities.Enums;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Controllers;

/// <summary>Ödeme adımı, 3D dönüşü ve teşekkür sayfası. Kapıda ödeme, havale; İyzico anahtarı varsa kart.</summary>
public partial class CheckoutController(
    ICartService cartService,
    IOrderService orderService,
    IPaymentService paymentService,
    IOptions<ShopSettings> shop,
    IOptions<ShippingSettings> shipping,
    IOptions<IyzicoSettings> iyzico,
    IConfiguration configuration,
    IPrivateFileStorage files) : Controller
{
    private ShopSettings Shop => shop.Value;

    private bool CardEnabled => iyzico.Value.IsConfigured;

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

        // Kart ödemesi reddedilip buraya dönüldüyse sebep TempData'dadır.
        return View(new CheckoutPageViewModel(
            new CheckoutFormViewModel(),
            cart,
            Shop.Iban,
            TempData[CartController.ErrorKey] as string,
            revalued > 0 ? PriceChangedMessage : null,
            CardEnabled));
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

        var byCard = form.PaymentMethod == PaymentMethod.KrediKarti;
        if (!(form.PaymentMethod is PaymentMethod.KapidaOdeme or PaymentMethod.HavaleEft || byCard && CardEnabled))
        {
            ModelState.AddModelError(nameof(form.PaymentMethod), "Şimdilik kapıda ödeme ve havale/EFT var.");
        }

        var card = byCard && CardEnabled ? ReadCard(form) : null;

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

        if (status == HttpStatusCode.Created && card is not null)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var (started, threeDs) = await paymentService.StartAsync(result.Data!.Id, card, ip, cancellationToken);
            if (started == HttpStatusCode.OK)
            {
                return View("ThreeDs", new ThreeDsViewModel(threeDs.Data!));
            }

            Response.StatusCode = (int)started;
            return Invalid(form, cart, threeDs.Message, setStatus: false);
        }

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

    /// <summary>İyzico 3D dönüşü. İstek sağlayıcı sayfasından tarayıcıyla gelir: çerez ve antiforgery anahtarı taşımaz,
    /// bu yüzden doğrulama imza + conversation_id + tutarla yapılır. Aynı dönüş tekrar gelirse ilk sonuç döner.</summary>
    [HttpPost("odeme/3d-donus")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ThreeDsReturn(
        [FromForm] string? status,
        [FromForm] string? paymentId,
        [FromForm] string? conversationId,
        [FromForm] string? conversationData,
        [FromForm] string? mdStatus,
        [FromForm] string? signature,
        CancellationToken cancellationToken)
    {
        var (outcome, result) = await paymentService.CompleteAsync(
            new PaymentCallback(status, paymentId, conversationId, conversationData, mdStatus, signature),
            cancellationToken);

        if (outcome == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (outcome == HttpStatusCode.OK)
        {
            var order = result.Data!;
            LastOrderCookie.Write(HttpContext, order.AccessToken);
            return SeeOther($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}");
        }

        TempData[CartController.ErrorKey] = result.Message;
        return SeeOther("/odeme");
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
        return View(new ThankYouViewModel(
            result.Data,
            whatsAppBase + "?text=" + Uri.EscapeDataString(message),
            Shop.Iban,
            shipping.Value.TrackingUrl(result.Data.Order.Carrier, result.Data.Order.TrackingNo)));
    }

    /// <summary>Havale bildirimi sonucu; teşekkür sayfasında bir kez gösterilir.</summary>
    public const string NoticeKey = "havale-bildirimi";

    /// <summary>Fatura yalnız siparişin anahtarıyla iner; başka siparişin anahtarı ya da anahtarsız istek 404.</summary>
    [HttpGet("siparis/{orderNo}/fatura")]
    public async Task<IActionResult> Invoice(string orderNo, [FromQuery(Name = "t")] string? t, CancellationToken cancellationToken)
    {
        var (status, result) = await orderService.GetByOrderNoAsync(orderNo, cancellationToken);
        return status == HttpStatusCode.OK
               && Guid.TryParse(t, out var supplied) && supplied == result.Data!.Order.AccessToken
               && files.Resolve(result.Data.Order.InvoiceFile) is { } path
            ? PhysicalFile(path, "application/pdf", $"fatura-{result.Data.Order.OrderNo}.pdf")
            : NotFound();
    }

    [HttpPost("siparis/{orderNo}/odeme-bildir")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(PrivateFileStorage.MaxBytes + 64_000)]
    public async Task<IActionResult> PaymentNotice(
        string orderNo,
        [FromForm(Name = "t")] Guid t,
        [FromForm] string? senderName,
        [FromForm] DateTime? paidOn,
        [FromForm] decimal amount,
        IFormFile? receipt,
        CancellationToken cancellationToken)
    {
        var thankYou = $"/siparis/{orderNo}/tesekkur?t={t}";
        string? stored = null;
        if (receipt is { Length: > 0 })
        {
            using var buffer = new MemoryStream();
            await receipt.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            if (receipt.Length > PrivateFileStorage.MaxBytes || PrivateFileStorage.Kind(bytes) is not { } kind)
            {
                TempData[NoticeKey] = "Dekont PDF ya da fotoğraf (PNG, JPEG, WebP) olmalı, en çok 5 MB.";
                return SeeOther(thankYou);
            }

            stored = await files.SaveAsync("dekontlar", kind, bytes, cancellationToken);
        }

        var (status, result) = await orderService.SubmitPaymentNoticeAsync(
            orderNo,
            t,
            new PaymentNoticeDraft(senderName ?? string.Empty, paidOn ?? DateTime.MinValue, amount, stored),
            cancellationToken);

        if (status != HttpStatusCode.Created)
        {
            files.Delete(stored);
            if (status == HttpStatusCode.NotFound)
            {
                return NotFound();
            }
        }

        TempData[NoticeKey] = result.Message;
        return SeeOther(thankYou);
    }

    private IActionResult Invalid(CheckoutFormViewModel form, CartView cart, string? message, bool setStatus = true)
    {
        if (setStatus)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        return View("Index", new CheckoutPageViewModel(form, cart, Shop.Iban, message, CardEnabled: CardEnabled));
    }

    private IActionResult SeeOther(string location)
    {
        Response.Headers.Location = location;
        return StatusCode(StatusCodes.Status303SeeOther);
    }

    /// <summary>Kart alanlarını biçimce denetler; hatalı alanı ModelState'e yazıp null döner. Değerler hata iletisine girmez.</summary>
    private PaymentCard? ReadCard(CheckoutFormViewModel form)
    {
        var number = CardSeparators().Replace(form.CardNumber ?? string.Empty, string.Empty);
        var holder = form.CardHolderName?.Trim() ?? string.Empty;
        var year = form.CardExpireYear?.Trim() ?? string.Empty;
        var valid = true;

        void Fail(string field, string message)
        {
            ModelState.AddModelError(field, message);
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(form.Email))
        {
            Fail(nameof(form.Email), "Kartla ödemede e-posta gerekli; ödeme onayı oraya gider.");
        }

        if (holder.Length is 0 or > 100)
        {
            Fail(nameof(form.CardHolderName), "Kart üzerindeki adı yazın.");
        }

        if (!Digits().IsMatch(number) || number.Length is < 12 or > 19)
        {
            Fail(nameof(form.CardNumber), "Kart numarasını kontrol edin.");
        }

        if (!int.TryParse(form.CardExpireMonth, out var month) || month is < 1 or > 12
            || !Digits().IsMatch(year) || year.Length is not (2 or 4))
        {
            Fail(nameof(form.CardExpireMonth), "Son kullanma tarihini AA / YY biçiminde yazın.");
        }

        if (!Digits().IsMatch(form.CardCvc ?? string.Empty) || form.CardCvc!.Length is < 3 or > 4)
        {
            Fail(nameof(form.CardCvc), "Kartın arkasındaki 3 haneli güvenlik kodunu yazın.");
        }

        return valid
            ? new PaymentCard(holder, number, month.ToString("00"), year.Length == 2 ? "20" + year : year, form.CardCvc!)
            : null;
    }

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex CardSeparators();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex Digits();

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
