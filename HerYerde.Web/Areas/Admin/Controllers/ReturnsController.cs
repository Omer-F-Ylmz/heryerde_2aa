using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>/admin/iadeler: iade/değişim talepleri; onay, gerekçeli ret, teslim alma, elle geri ödeme. İşlemler denetim izine yazılır.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class ReturnsController(IReturnService returnService, IAdminAuditService auditService, IPrivateFileStorage files) : Controller
{
    [HttpGet("admin/iadeler")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (_, list) = await returnService.GetAllAsync(cancellationToken);
        return View(list.Data!);
    }

    [HttpGet("admin/iadeler/{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
        => await DetailPageAsync(id, HttpStatusCode.OK, null, cancellationToken);

    [HttpGet("admin/iadeler/{id:int}/foto")]
    public async Task<IActionResult> Photo(int id, CancellationToken cancellationToken)
    {
        var (status, detail) = await returnService.GetByIdAsync(id, cancellationToken);
        return status == HttpStatusCode.OK && files.Resolve(detail.Data!.Request.PhotoFile) is { } path
            ? PhysicalFile(path, PrivateFileStorage.ContentType(detail.Data.Request.PhotoFile!))
            : NotFound();
    }

    [HttpPost("admin/iadeler/{id:int}/onayla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
        => await ActAsync(id, "iade onayı", await returnService.ApproveAsync(id, cancellationToken), cancellationToken);

    [HttpPost("admin/iadeler/{id:int}/reddet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason, CancellationToken cancellationToken)
        => await ActAsync(id, "iade reddi", await returnService.RejectAsync(id, reason ?? string.Empty, cancellationToken), cancellationToken);

    [HttpPost("admin/iadeler/{id:int}/teslim-al")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(int id, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return await ActAsync(id, "iade teslim alındı", await returnService.ReceiveAsync(id, ip, cancellationToken), cancellationToken);
    }

    [HttpPost("admin/iadeler/{id:int}/geri-odendi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRefunded(int id, CancellationToken cancellationToken)
        => await ActAsync(id, "iade geri ödendi", await returnService.MarkRefundedAsync(id, cancellationToken), cancellationToken);

    private async Task<IActionResult> ActAsync(int id, string action, (HttpStatusCode Status, HerYerde.Core.Utilities.Results.IResult Result) outcome, CancellationToken cancellationToken)
    {
        if (outcome.Status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (outcome.Status != HttpStatusCode.OK)
        {
            return await DetailPageAsync(id, outcome.Status, outcome.Result.Message, cancellationToken);
        }

        await auditService.WriteAsync(HttpContext, action, "iade", id);
        return Redirect($"/admin/iadeler/{id}");
    }

    private async Task<IActionResult> DetailPageAsync(int id, HttpStatusCode status, string? error, CancellationToken cancellationToken)
    {
        var (found, detail) = await returnService.GetByIdAsync(id, cancellationToken);
        if (found != HttpStatusCode.OK)
        {
            return NotFound();
        }

        Response.StatusCode = (int)status;
        return View("Detail", new ReturnDetailViewModel(detail.Data!, error));
    }
}
