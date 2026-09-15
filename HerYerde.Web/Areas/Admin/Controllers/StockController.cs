using HerYerde.Business;
using HerYerde.Business.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>/admin/stok: stoğu Shop:LowStockAlertAt ve altına inmiş ürün ve varyantlar, en azı önce.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class StockController(IProductService productService, IOptions<ShopSettings> shop) : Controller
{
    [HttpGet("admin/stok")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (_, rows) = await productService.GetLowStockAsync(shop.Value.LowStockAlertAt, cancellationToken);
        ViewData["Threshold"] = shop.Value.LowStockAlertAt;
        return View(rows.Data!);
    }
}
