using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Markalar: liste, ekleme/düzenleme (isteğe bağlı logo), ürünü olmayan markanın silinmesi.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class BrandsController(IBrandService brands, IAdminAuditService auditService, IProductImageStorage imageStorage) : Controller
{
    private const string Entity = "marka";

    [HttpGet("admin/markalar")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await brands.GetAllAsync(cancellationToken));

    [HttpGet("admin/markalar/yeni")]
    public IActionResult Create() => View("Form", new BrandFormViewModel());

    [HttpPost("admin/markalar/yeni")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(BrandFormViewModel form, CancellationToken cancellationToken)
    {
        form.Id = 0;
        return SaveAsync(form, cancellationToken);
    }

    [HttpGet("admin/markalar/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        => await brands.GetByIdAsync(id, cancellationToken) is { } brand
            ? View("Form", new BrandFormViewModel { Id = brand.Id, Name = brand.Name, LogoUrl = brand.LogoUrl })
            : NotFound();

    [HttpPost("admin/markalar/{id:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, BrandFormViewModel form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return SaveAsync(form, cancellationToken);
    }

    [HttpPost("admin/markalar/{id:int}/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var stored = await brands.GetByIdAsync(id, cancellationToken);
        var (status, result) = await brands.DeleteAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            TempData["MarkaHata"] = result.Message;
            return status == HttpStatusCode.NotFound ? NotFound() : RedirectToAction(nameof(Index));
        }

        if (stored?.LogoUrl is { } logo)
        {
            imageStorage.DeleteBrandLogo(logo);
        }

        await auditService.WriteAsync(HttpContext, "marka silme", Entity, id);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(BrandFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Form", form);
        }

        if (form.Logo is { } logo && await ImageFile.ProblemAsync(logo, cancellationToken) is { } problem)
        {
            form.ErrorMessage = problem;
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Form", form);
        }

        var previous = form.Id == 0 ? null : (await brands.GetByIdAsync(form.Id, cancellationToken))?.LogoUrl;
        var (status, saved) = await brands.SaveAsync(new Brand { Id = form.Id, Name = form.Name, LogoUrl = previous }, cancellationToken);
        if (status is not (HttpStatusCode.OK or HttpStatusCode.Created))
        {
            form.ErrorMessage = saved.Message;
            Response.StatusCode = (int)status;
            return View("Form", form);
        }

        var brand = saved.Data!;
        if (form.Logo is { } upload)
        {
            // Logo adresi marka kimliğiyle klasörlenir: yeni markada kayıttan sonra yazılır.
            await using var content = upload.OpenReadStream();
            brand.LogoUrl = await imageStorage.SaveBrandLogoAsync(brand.Id, content, cancellationToken);
            await brands.SaveAsync(brand, cancellationToken);
            if (previous is not null)
            {
                imageStorage.DeleteBrandLogo(previous);
            }
        }

        await auditService.WriteAsync(HttpContext, status == HttpStatusCode.Created ? "marka ekle" : "marka güncelle", Entity, brand.Id);
        return RedirectToAction(nameof(Index));
    }
}
