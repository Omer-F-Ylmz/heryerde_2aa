using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>İndirim kuponları: liste (kullanım sayısıyla), ekleme/düzenleme, silme.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class CouponsController(ICouponService coupons, IAdminAuditService auditService) : Controller
{
    [HttpGet("admin/kuponlar")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (_, result) = await coupons.GetForAdminAsync(cancellationToken);
        return View(result.Data!);
    }

    [HttpGet("admin/kuponlar/yeni")]
    public IActionResult Create() => View("Form", new CouponFormViewModel());

    [HttpPost("admin/kuponlar/yeni")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(CouponFormViewModel form, CancellationToken cancellationToken)
        => SaveAsync(form, cancellationToken);

    [HttpGet("admin/kuponlar/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var (status, result) = await coupons.GetByIdAsync(id, cancellationToken);
        return status == HttpStatusCode.OK ? View("Form", CouponFormViewModel.From(result.Data!)) : NotFound();
    }

    [HttpPost("admin/kuponlar/{id:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, CouponFormViewModel form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return SaveAsync(form, cancellationToken);
    }

    [HttpPost("admin/kuponlar/{id:int}/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var (status, _) = await coupons.DeleteAsync(id, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        await auditService.WriteAsync(HttpContext, "kupon silme", "kupon", id);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(CouponFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Form", form);
        }

        var (status, result) = await coupons.SaveAsync(
            new Coupon
            {
                Id = form.Id,
                Code = form.Code,
                Kind = form.Kind,
                Value = form.Value,
                MinSubtotal = form.MinSubtotal,
                StartsAt = form.StartsAt,
                // Bitiş günü dahil: kayıt ertesi günün başında biter.
                EndsAt = form.EndsAt.Date.AddDays(1),
                TotalLimit = form.TotalLimit,
                PerPersonLimit = form.PerPersonLimit,
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

        await auditService.WriteAsync(HttpContext, form.Id > 0 ? "kupon güncelleme" : "kupon ekleme", "kupon", result.Data!.Id, result.Data.Code);
        return RedirectToAction(nameof(Index));
    }
}
