using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using HerYerde.Business.Abstract;
using HerYerde.Business.Concrete;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>/admin/kvkk: ilgili kişi başvurusu aracı. Arama POST ile (telefon/e-posta adres satırına ve tarayıcı geçmişine girmez);
/// her işlem kişi maskelenmiş olarak denetim izine yazılır.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class KvkkController(
    IKvkkService kvkkService,
    IAdminAuditService auditService,
    IPrivateFileStorage files,
    TimeProvider clock) : Controller
{
    private static readonly JsonSerializerOptions DumpJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [HttpGet("admin/kvkk")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await PageAsync(null, null, null, cancellationToken));

    [HttpPost("admin/kvkk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(string? ara, CancellationToken cancellationToken)
    {
        var (status, person) = await kvkkService.FindAsync(ara ?? string.Empty, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View("Index", await PageAsync(ara, null, person.Message, cancellationToken));
        }

        var found = person.Data!;
        await auditService.WriteAsync(HttpContext, "kvkk arama", "kvkk", null,
            $"{KvkkManager.Masked(found.Subject)} · {found.Orders.Count} sipariş, {found.ContactMessages.Count} mesaj");
        return View("Index", await PageAsync(ara, found, null, cancellationToken));
    }

    [HttpPost("admin/kvkk/dokum-json")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DumpJson(string? ara, CancellationToken cancellationToken)
    {
        var (status, person) = await kvkkService.FindAsync(ara ?? string.Empty, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return BadRequest(person.Message);
        }

        await auditService.WriteAsync(HttpContext, "kvkk erişim dökümü", "kvkk", null, KvkkManager.Masked(person.Data!.Subject) + " · json");
        return File(JsonSerializer.SerializeToUtf8Bytes(person.Data, DumpJsonOptions), "application/json", $"kvkk-dokum-{clock.GetUtcNow():yyyyMMdd}.json");
    }

    [HttpPost("admin/kvkk/dokum-pdf")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DumpPdf(string? ara, CancellationToken cancellationToken)
    {
        var (status, person) = await kvkkService.FindAsync(ara ?? string.Empty, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return BadRequest(person.Message);
        }

        await auditService.WriteAsync(HttpContext, "kvkk erişim dökümü", "kvkk", null, KvkkManager.Masked(person.Data!.Subject) + " · pdf");
        return File(KvkkPdf.Dump(person.Data, clock.GetUtcNow().UtcDateTime), "application/pdf", $"kvkk-dokum-{clock.GetUtcNow():yyyyMMdd}.pdf");
    }

    [HttpPost("admin/kvkk/anonimlestir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anonymize(string? ara, CancellationToken cancellationToken)
    {
        var (status, result) = await kvkkService.AnonymizeAllAsync(ara ?? string.Empty, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View("Index", await PageAsync(ara, null, result.Message, cancellationToken));
        }

        var outcome = result.Data!;
        foreach (var file in outcome.Files)
        {
            files.Delete(file);
        }

        foreach (var order in outcome.AnonymizedOrders)
        {
            await auditService.ForgetDetailsAsync("sipariş", order.Id, CancellationToken.None);
            await auditService.WriteAsync(HttpContext, "kişisel veri anonimleştirildi", "sipariş", order.Id);
        }

        await auditService.WriteAsync(HttpContext, "kvkk anonimleştirme", "kvkk", null,
            $"{KvkkManager.Masked(KvkkManager.Subject(ara)!)} · {outcome.AnonymizedOrders.Count} sipariş, {outcome.DeletedMessages} mesaj, "
            + $"{outcome.MaskedReviews} yorum, atlanan: {(outcome.SkippedOrderNos.Count == 0 ? "yok" : string.Join(", ", outcome.SkippedOrderNos))}");
        return View("Index", await PageAsync(null, null, null, cancellationToken, result.Message));
    }

    [HttpPost("admin/kvkk/basvuru")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenRequest(string? subject, DateTime? receivedOn, CancellationToken cancellationToken)
    {
        var receivedAt = receivedOn is { } day ? IstanbulTimeUtc(day) : clock.GetUtcNow().UtcDateTime;
        var (status, result) = await kvkkService.OpenRequestAsync(subject ?? string.Empty, receivedAt, cancellationToken);
        if (status != HttpStatusCode.Created)
        {
            Response.StatusCode = (int)status;
            return View("Index", await PageAsync(null, null, result.Message, cancellationToken));
        }

        await auditService.WriteAsync(HttpContext, "kvkk başvurusu", "kvkk", result.Data!.Id, KvkkManager.Masked(result.Data.Subject));
        return Redirect("/admin/kvkk");
    }

    [HttpPost("admin/kvkk/basvuru/{id:int}/tamamla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRequest(int id, CancellationToken cancellationToken)
    {
        var (status, _) = await kvkkService.CompleteRequestAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        await auditService.WriteAsync(HttpContext, "kvkk başvurusu yanıtlandı", "kvkk", id);
        return Redirect("/admin/kvkk");
    }

    private static DateTime IstanbulTimeUtc(DateTime day) => HerYerde.Web.Models.IstanbulTime.StartOfDayUtc(DateOnly.FromDateTime(day));

    private async Task<KvkkPageViewModel> PageAsync(
        string? query,
        HerYerde.Business.Dtos.KvkkPerson? person,
        string? error,
        CancellationToken cancellationToken,
        string? notice = null)
    {
        var (_, requests) = await kvkkService.GetRequestsAsync(cancellationToken);
        return new KvkkPageViewModel(query, person, requests.Data!, error, notice);
    }
}
