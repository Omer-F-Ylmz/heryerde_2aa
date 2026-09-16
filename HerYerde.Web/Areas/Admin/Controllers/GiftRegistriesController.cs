using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Çeyiz listeleri: ilerleme görünümü ve silme. Yönetim anahtarı panelde gösterilmez; liste sahibinindir.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class GiftRegistriesController(IGiftRegistryService registries, IAdminAuditService auditService) : Controller
{
    [HttpGet("admin/ceyizlistesi")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await registries.GetAllAsync(cancellationToken));

    [HttpPost("admin/ceyizlistesi/{id:int}/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var (status, _) = await registries.DeleteAsync(id, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        await auditService.WriteAsync(HttpContext, "çeyiz listesi silme", "çeyiz listesi", id);
        return RedirectToAction(nameof(Index));
    }
}
