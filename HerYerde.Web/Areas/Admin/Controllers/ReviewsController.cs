using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Ürün yorumları: onay bekleyenler önce; onay yayınlar, ret kaydı siler. İşlemler denetim izine yazılır.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class ReviewsController(IReviewService reviewService, IAdminAuditService auditService) : Controller
{
    [HttpGet("admin/yorumlar")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (_, reviews) = await reviewService.GetForAdminAsync(cancellationToken);
        return View(reviews.Data!);
    }

    [HttpPost("admin/yorumlar/{id:int}/onayla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var (status, _) = await reviewService.ApproveAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        await auditService.WriteAsync(HttpContext, "onayla", "yorum", id);
        return Redirect("/admin/yorumlar");
    }

    [HttpPost("admin/yorumlar/{id:int}/reddet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken)
    {
        var (status, _) = await reviewService.RejectAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        await auditService.WriteAsync(HttpContext, "reddet", "yorum", id);
        return Redirect("/admin/yorumlar");
    }
}
