using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Çıplak /admin adresi: yetkiliyi ürün listesine yollar, yetkisizi giriş sayfasına.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Products");
}
