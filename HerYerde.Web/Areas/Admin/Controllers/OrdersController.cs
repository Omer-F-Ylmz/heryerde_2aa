using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Rules;
using HerYerde.Entities.Enums;
using HerYerde.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;
    private readonly TimeProvider _clock;

    public OrdersController(IOrderService orderService, TimeProvider clock)
    {
        _orderService = orderService;
        _clock = clock;
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

        return View(new OrderDetailViewModel { Detail = detail.Data! });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, OrderStatus next, CancellationToken cancellationToken)
    {
        var (status, result) = await _orderService.ChangeStatusAsync(id, next, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK)
        {
            var (_, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
            Response.StatusCode = (int)status;
            return View("Detail", new OrderDetailViewModel { Detail = detail.Data!, ErrorMessage = result.Message });
        }

        return RedirectToAction(nameof(Detail), new { id });
    }
}
