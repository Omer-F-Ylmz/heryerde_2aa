using HerYerde.Business.Abstract;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>/siparis-sorgula: sipariş numarası + telefon eşleşirse token'lı sipariş sayfasına 303. Yanlış numara, yanlış
/// telefon ve bot tuzağı aynı yanıtı alır; deneme sayısı hız sınırındadır (RateLimit:OrderLookupPerMinute).</summary>
public class OrderLookupController(IOrderService orderService) : Controller
{
    public const string NotFoundMessage = "Bu sipariş numarası ve telefonla eşleşen sipariş bulunamadı. Numarayı onay e-postanızdan ya da WhatsApp mesajından kontrol edin.";

    [HttpGet("siparis-sorgula")]
    public IActionResult Index() => View(new OrderLookupViewModel());

    [HttpPost("siparis-sorgula")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Find(OrderLookupViewModel form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(form.Website) && !string.IsNullOrWhiteSpace(form.OrderNo) && !string.IsNullOrWhiteSpace(form.Phone))
        {
            var (_, found) = await orderService.LookupAsync(form.OrderNo, form.Phone, cancellationToken);
            if (found.Data is { } order)
            {
                Response.Headers.Location = $"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}";
                return StatusCode(StatusCodes.Status303SeeOther);
            }
        }

        return View("Index", new OrderLookupViewModel { OrderNo = form.OrderNo, ErrorMessage = NotFoundMessage });
    }
}
