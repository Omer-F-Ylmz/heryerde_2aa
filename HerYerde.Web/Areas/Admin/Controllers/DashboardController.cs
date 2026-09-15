using HerYerde.Business.Abstract;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>/admin panosu: günün ve haftanın siparişi/cirosu, bekleyen işler (havale, iade, yorum, mesaj), düşük stok, gecikmiş kargo.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class DashboardController(IDashboardService dashboardService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await dashboardService.GetAsync(IstanbulTime.Zone, cancellationToken));
}
