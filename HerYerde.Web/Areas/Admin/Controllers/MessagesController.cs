using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>İletişim formu mesajları: liste, okundu işareti, silme; işlemler denetim izine yazılır.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class MessagesController(IContactService contactService, IAdminAuditService auditService) : Controller
{
    [HttpGet("admin/mesajlar")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (_, messages) = await contactService.GetRecentAsync(cancellationToken);
        return View(messages.Data!);
    }

    [HttpPost("admin/mesajlar/{id:int}/okundu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var (status, _) = await contactService.MarkReadAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        await auditService.WriteAsync(HttpContext, "okundu", "mesaj", id);
        return Redirect("/admin/mesajlar");
    }

    [HttpPost("admin/mesajlar/{id:int}/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var (status, _) = await contactService.DeleteAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        await auditService.WriteAsync(HttpContext, "sil", "mesaj", id);
        return Redirect("/admin/mesajlar");
    }
}
