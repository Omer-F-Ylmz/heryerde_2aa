using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
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
    IPrivateFileStorage files,
    ILegalPdfArchive legalPdfs,
    IReturnService returnService,
    IAdminAuditService auditService,
    IInstallmentService installments,
    IProvinceDirectory provinces,
    TimeProvider clock) : Controller
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
            CardEnabled,
            CardEnabled && installments.Enabled));
    }

    /// <summary>İl seçilince ilçe listesini tazeler. JS'siz de çalışsın diye POST: kart alanları sorgu dizesine düşmez,
    /// sayfaya da geri yazılmaz. Sipariş açılmaz, yalnız form yeniden çizilir.</summary>
    [HttpPost("odeme/ilce")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Districts(CheckoutFormViewModel form, CancellationToken cancellationToken)
    {
        var cart = await CurrentCartAsync(cancellationToken);
        if (cart is null || cart.IsEmpty)
        {
            return Redirect("/sepet");
        }

        // İl değişince eski ilçe listede kalmasın.
        if (!provinces.DistrictsOf(form.City).Contains(form.District))
        {
            form.District = string.Empty;
        }

        ModelState.Clear();
        return View("Index", new CheckoutPageViewModel(
            form,
            cart,
            Shop.Iban,
            null,
            CardEnabled: CardEnabled,
            InstallmentsEnabled: CardEnabled && installments.Enabled));
    }

    /// <summary>Kart numarasının ilk 6 hanesine göre taksit tablosu; sepet toplamı üzerinden. Tablo kapalıysa boş döner.</summary>
    [HttpGet("odeme/taksit")]
    public async Task<IActionResult> Installments(string? bin, CancellationToken cancellationToken)
    {
        var cart = await CurrentCartAsync(cancellationToken);
        if (!CardEnabled || cart is null || cart.IsEmpty)
        {
            return PartialView("Components/_Installments", new InstallmentTableVm([]));
        }

        var table = await installments.GetAsync(cart.Total, bin, cancellationToken);
        return PartialView("Components/_Installments", table is null
            ? new InstallmentTableVm([])
            : new InstallmentTableVm(
                table.Options,
                table.BankName,
                "Taksit tutarları bankanız tarafından belirlenir; çekim onayladığınız taksitle yapılır."));
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

        return await ThankYouPageAsync(result.Data, null, cancellationToken);
    }

    /// <summary>Havale bildirimi sonucu; teşekkür sayfasında bir kez gösterilir.</summary>
    public const string NoticeKey = "havale-bildirimi";

    /// <summary>Müşteri iptali ve iade talebi sonucu; teşekkür sayfasında bir kez gösterilir.</summary>
    public const string OrderMessageKey = "siparis-islem";

    /// <summary>Teslim edilmiş siparişin iade/değişim formu; talep açılamıyorsa sipariş sayfasına nedeniyle döner.</summary>
    [HttpGet("siparis/{orderNo}/iade")]
    public async Task<IActionResult> ReturnRequest(string orderNo, [FromQuery(Name = "t")] Guid t, CancellationToken cancellationToken)
    {
        var (status, form) = await returnService.GetFormAsync(orderNo, t, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK || form.Data!.Lines.All(l => l.Returnable == 0))
        {
            TempData[OrderMessageKey] = status == HttpStatusCode.OK ? "Bu siparişin tüm ürünleri için talep açılmış." : form.Message;
            return SeeOther($"/siparis/{orderNo}/tesekkur?t={t}");
        }

        return View(new ReturnRequestPageViewModel(form.Data, null));
    }

    [HttpPost("siparis/{orderNo}/iade")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(PrivateFileStorage.MaxBytes + 64_000)]
    public async Task<IActionResult> ReturnRequest(string orderNo, ReturnRequestFormModel form, IFormFile? photo, CancellationToken cancellationToken)
    {
        string? stored = null;
        string? error = null;
        if (photo is { Length: > 0 })
        {
            using var buffer = new MemoryStream();
            await photo.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            if (photo.Length > PrivateFileStorage.MaxBytes || PrivateFileStorage.Kind(bytes) is not ("png" or "jpg" or "webp"))
            {
                error = "Fotoğraf PNG, JPEG ya da WebP olmalı, en çok 5 MB.";
            }
            else
            {
                stored = await files.SaveAsync("iadeler", PrivateFileStorage.Kind(bytes)!, bytes, cancellationToken);
            }
        }

        var status = HttpStatusCode.BadRequest;
        if (error is null)
        {
            var draft = new ReturnDraft(
                form.Type,
                form.Reason ?? string.Empty,
                form.Lines.Select(l => new ReturnLine(l.OrderItemId, l.Quantity, form.Type == ReturnType.Degisim ? l.NewSku : null)).ToList(),
                form.Iban,
                stored);
            (status, var result) = await returnService.RequestAsync(orderNo, form.T, draft, cancellationToken);
            if (status == HttpStatusCode.Created)
            {
                TempData[OrderMessageKey] = result.Message;
                return SeeOther($"/siparis/{orderNo}/tesekkur?t={form.T}");
            }

            files.Delete(stored);
            error = result.Message;
        }

        var (found, page) = await returnService.GetFormAsync(orderNo, form.T, cancellationToken);
        if (found != HttpStatusCode.OK)
        {
            return found == HttpStatusCode.NotFound ? NotFound() : SeeOther($"/siparis/{orderNo}/tesekkur?t={form.T}");
        }

        Response.StatusCode = (int)status;
        return View(new ReturnRequestPageViewModel(page.Data!, error));
    }

    /// <summary>Müşteri iptali; başarısızsa sipariş sayfası nedeni ve durum koduyla yeniden çizilir.</summary>
    [HttpPost("siparis/{orderNo}/iptal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string orderNo, [FromForm(Name = "t")] Guid t, [FromForm] string? iban, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var (status, result) = await returnService.CancelByCustomerAsync(orderNo, t, iban, ip, cancellationToken);
        var (found, detail) = await orderService.GetByOrderNoAsync(orderNo, cancellationToken);
        if (status == HttpStatusCode.NotFound || found != HttpStatusCode.OK)
        {
            return NotFound();
        }

        if (status == HttpStatusCode.OK)
        {
            // Yönetici olmayan işlem: izde yönetici kimliği 0 ("Müşteri"), siparişin zaman çizelgesinde görünür.
            await auditService.WriteAsync(HttpContext, "müşteri iptali", "sipariş", detail.Data!.Order.Id);
            TempData[OrderMessageKey] = result.Message;
            return SeeOther($"/siparis/{orderNo}/tesekkur?t={t}");
        }

        Response.StatusCode = (int)status;
        return await ThankYouPageAsync(detail.Data!, result.Message, cancellationToken);
    }

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

    /// <summary>Siparişte onaylanan sürümün ön bilgilendirme + sözleşme PDF'i; yalnız siparişin anahtarıyla, arşivlenmemiş eski sürümde 404.</summary>
    [HttpGet("siparis/{orderNo}/sozlesme")]
    public async Task<IActionResult> Contract(string orderNo, [FromQuery(Name = "t")] string? t, CancellationToken cancellationToken)
    {
        var (status, result) = await orderService.GetByOrderNoAsync(orderNo, cancellationToken);
        return status == HttpStatusCode.OK
               && Guid.TryParse(t, out var supplied) && supplied == result.Data!.Order.AccessToken
               && result.Data.Order.LegalVersion is { } version
               && await legalPdfs.ResolveAsync(LegalDocs.ArchivePath(version), cancellationToken) is { } path
            ? PhysicalFile(path, "application/pdf", $"sozlesme-{result.Data.Order.OrderNo}.pdf")
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

    private async Task<IActionResult> ThankYouPageAsync(OrderDetail detail, string? error, CancellationToken cancellationToken)
    {
        var order = detail.Order;
        // Talep bağlantısı süre içindeyken ve talep edilmemiş adet kaldıkça görünür.
        var canRequest = ReturnRules.CanRequest(order, clock.GetUtcNow().UtcDateTime)
                         && (await returnService.GetFormAsync(order.OrderNo, order.AccessToken, cancellationToken)).Item2.Data?.Lines.Any(l => l.Returnable > 0) == true;
        var whatsAppBase = configuration["Shop:WhatsApp"] ?? "https://wa.me/";
        var message = $"Merhaba, {order.OrderNo} numaralı siparişimi bildirmek istiyorum.";
        return View("ThankYou", new ThankYouViewModel(
            detail,
            whatsAppBase + "?text=" + Uri.EscapeDataString(message),
            Shop.Iban,
            shipping.Value.TrackingUrl(order.Carrier, order.TrackingNo),
            await returnService.GetForOrderAsync(order.Id, cancellationToken),
            canRequest,
            ReturnRules.CanCustomerCancel(order.Status),
            error));
    }

    private IActionResult Invalid(CheckoutFormViewModel form, CartView cart, string? message, bool setStatus = true)
    {
        if (setStatus)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        return View("Index", new CheckoutPageViewModel(
            form,
            cart,
            Shop.Iban,
            message,
            CardEnabled: CardEnabled,
            InstallmentsEnabled: CardEnabled && installments.Enabled));
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
