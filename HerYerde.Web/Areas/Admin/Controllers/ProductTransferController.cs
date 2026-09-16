using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business.Abstract;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Toplu ürün: .xlsx dışa aktarma, boş şablon, içe aktarma (önizleme → onay).</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public partial class ProductTransferController(
    IProductTransferService transferService,
    IAdminAuditService auditService,
    IPrivateFileStorage files,
    IProductImageStorage imageStorage,
    TimeProvider clock) : Controller
{
    private const string Folder = "aktarimlar";

    /// <summary>Onaylanmamış önizleme dosyası bu süreden sonra silinir.</summary>
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromDays(1);

    [HttpGet("admin/products/export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var rows = await transferService.ExportAsync(cancellationToken);
        await auditService.WriteAsync(HttpContext, "ürün dışa aktarma", "ürün", null, $"{rows.Count} satır");
        return File(ProductSheet.Write(rows), ProductSheet.ContentType, $"urunler-{clock.GetUtcNow():yyyyMMdd}.xlsx");
    }

    [HttpGet("admin/products/export/sablon")]
    public IActionResult Template() => File(ProductSheet.Template(), ProductSheet.ContentType, "urun-sablonu.xlsx");

    [HttpGet("admin/products/import")]
    public IActionResult Import() => View(new ProductImportViewModel());

    [HttpPost("admin/products/import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ProductSheet.MaxBytes + 64_000)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > ProductSheet.MaxBytes)
        {
            return Invalid("En çok 8 MB'lık bir .xlsx dosyası seçin.");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var (rows, error) = ProductSheet.Read(bytes);
        if (rows is null)
        {
            return Invalid(error!);
        }

        files.DeleteOlderThan(Folder, PreviewLifetime, clock.GetUtcNow().UtcDateTime);
        var stored = await files.SaveAsync(Folder, "xlsx", bytes, cancellationToken);
        return View(new ProductImportViewModel
        {
            Preview = await transferService.PreviewAsync(rows, cancellationToken),
            Token = Path.GetFileNameWithoutExtension(stored)
        });
    }

    [HttpPost("admin/products/import/onayla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string? token, CancellationToken cancellationToken)
    {
        var relative = token is not null && TokenPattern().IsMatch(token) ? $"{Folder}/{token}.xlsx" : null;
        if (files.Resolve(relative) is not { } path)
        {
            return Invalid("Önizleme bulunamadı ya da süresi geçti; dosyayı yeniden yükleyin.");
        }

        var (rows, error) = ProductSheet.Read(await System.IO.File.ReadAllBytesAsync(path, cancellationToken));
        if (rows is null)
        {
            return Invalid(error!);
        }

        var (status, result) = await transferService.ApplyAsync(rows, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View(nameof(Import), new ProductImportViewModel { Preview = result.Data, Token = token, ErrorMessage = result.Message });
        }

        // Listeden çıkan yüklenmiş görselin dosyası da diskten silinir; dış adres ya da elle yazılmış yol Delete'te eşleşmez.
        foreach (var url in result.Data!.Removed)
        {
            imageStorage.Delete(url);
        }

        files.Delete(relative);
        await auditService.WriteAsync(HttpContext, "ürün içe aktarma", "ürün", null, result.Message);
        TempData[ImportNoticeKey] = "İçe aktarma tamamlandı: " + result.Message;
        return Redirect("/admin/products");
    }

    public const string ImportNoticeKey = "ice-aktarma";

    private ViewResult Invalid(string message)
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(nameof(Import), new ProductImportViewModel { ErrorMessage = message });
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TokenPattern();
}
