using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Site üstündeki duyuru şeridi: liste, ekleme/düzenleme, silme.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class AnnouncementsController(IAnnouncementService announcements, IAdminAuditService auditService) : Controller
{
    [HttpGet("admin/duyurular")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (_, result) = await announcements.GetForAdminAsync(cancellationToken);
        return View(result.Data!);
    }

    [HttpGet("admin/duyurular/yeni")]
    public IActionResult Create() => View("Form", new AnnouncementFormViewModel());

    [HttpPost("admin/duyurular/yeni")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(AnnouncementFormViewModel form, CancellationToken cancellationToken)
        => SaveAsync(form, cancellationToken);

    [HttpGet("admin/duyurular/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var (status, result) = await announcements.GetByIdAsync(id, cancellationToken);
        return status == HttpStatusCode.OK ? View("Form", AnnouncementFormViewModel.From(result.Data!)) : NotFound();
    }

    [HttpPost("admin/duyurular/{id:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, AnnouncementFormViewModel form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return SaveAsync(form, cancellationToken);
    }

    [HttpPost("admin/duyurular/{id:int}/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var (status, _) = await announcements.DeleteAsync(id, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        await auditService.WriteAsync(HttpContext, "duyuru silme", "duyuru", id);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(AnnouncementFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Form", form);
        }

        var (status, result) = await announcements.SaveAsync(
            new Announcement
            {
                Id = form.Id,
                Text = form.Text,
                Url = form.Url,
                StartsAt = form.StartsAt,
                // Bitiş günü dahil olsun: kayıt ertesi günün başında biter.
                EndsAt = form.EndsAt.Date.AddDays(1),
                Color = form.Color,
                IsActive = form.IsActive
            },
            cancellationToken);

        if (status != HttpStatusCode.OK)
        {
            if (status == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            Response.StatusCode = (int)status;
            form.ErrorMessage = result.Message;
            return View("Form", form);
        }

        await auditService.WriteAsync(HttpContext, form.Id > 0 ? "duyuru güncelleme" : "duyuru ekleme", "duyuru", result.Data!.Id);
        return RedirectToAction(nameof(Index));
    }
}
