using System.Net;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Entities.Enums;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;
    private readonly TimeProvider _clock;
    private readonly IAdminAuditService _auditService;
    private readonly ShippingSettings _shipping;

    public OrdersController(
        IOrderService orderService,
        TimeProvider clock,
        IAdminAuditService auditService,
        IOptions<ShippingSettings> shipping)
    {
        _orderService = orderService;
        _clock = clock;
        _auditService = auditService;
        _shipping = shipping.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(OrderStatus? durum, string? ara, CancellationToken cancellationToken)
    {
        var (_, orders) = await _orderService.SearchAsync(durum, ara, cancellationToken);
        return View(new OrderListViewModel
        {
            Orders = orders.Data!,
            Status = durum,
            Query = ara,
            SampleOrderNo = OrderNo.Build(_clock.GetUtcNow().UtcDateTime, 1)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var (status, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        // Detayı açmak siparişi okunmuş sayar; başlıktaki rozet buradan düşer.
        await _orderService.MarkSeenAsync(id, cancellationToken);
        return View(ViewFor(detail.Data!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        int id,
        OrderStatus next,
        string? carrier,
        string? trackingNo,
        CancellationToken cancellationToken)
    {
        var (status, result) = await _orderService.ChangeStatusAsync(id, next, carrier, trackingNo, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK)
        {
            var (_, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
            Response.StatusCode = (int)status;
            return View("Detail", ViewFor(detail.Data!, result.Message));
        }

        await _auditService.WriteAsync(HttpContext, "durum: " + OrderLabels.For(next), "sipariş", id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anonymize(int id, CancellationToken cancellationToken)
    {
        var (status, result) = await _orderService.AnonymizeAsync(id, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK)
        {
            var (_, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
            Response.StatusCode = (int)status;
            return View("Detail", ViewFor(detail.Data!, result.Message));
        }

        await _auditService.WriteAsync(HttpContext, "kişisel veri anonimleştirildi", "sipariş", id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    private OrderDetailViewModel ViewFor(OrderDetail detail, string? errorMessage = null) => new()
    {
        Detail = detail,
        ErrorMessage = errorMessage,
        Carriers = _shipping.Carriers.Select(c => c.Name).ToList(),
        TrackingUrl = _shipping.TrackingUrl(detail.Order.Carrier, detail.Order.TrackingNo)
    };
}
