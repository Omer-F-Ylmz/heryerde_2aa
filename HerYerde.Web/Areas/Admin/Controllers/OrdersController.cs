using System.Net;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Entities.Enums;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly TimeProvider _clock;
    private readonly IAdminAuditService _auditService;
    private readonly IPrivateFileStorage _files;
    private readonly ShippingSettings _shipping;

    public OrdersController(
        IOrderService orderService,
        IPaymentService paymentService,
        TimeProvider clock,
        IAdminAuditService auditService,
        IPrivateFileStorage files,
        IOptions<ShippingSettings> shipping)
    {
        _orderService = orderService;
        _paymentService = paymentService;
        _clock = clock;
        _auditService = auditService;
        _files = files;
        _shipping = shipping.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(OrderStatus? durum, string? ara, int? faturasiz, CancellationToken cancellationToken)
    {
        var uninvoiced = faturasiz == 1;
        var (_, orders) = await _orderService.SearchAsync(durum, ara, uninvoiced, cancellationToken);
        return View(new OrderListViewModel
        {
            Orders = orders.Data!,
            Status = uninvoiced ? null : durum,
            Query = ara,
            Uninvoiced = uninvoiced,
            SampleOrderNo = OrderNo.Build(_clock.GetUtcNow().UtcDateTime, 1)
        });
    }

    /// <summary>bicim=kargo: kargo firmasına verilen sabit kolonlu şablon; bicim=tam (varsayılan): tüm alanlar. tarih: İstanbul günü.</summary>
    [HttpGet("admin/orders/export")]
    public async Task<IActionResult> Export(OrderStatus? durum, DateOnly? tarih, string? bicim, CancellationToken cancellationToken)
    {
        var cargo = bicim == "kargo";
        var orders = await _orderService.ExportAsync(
            durum,
            tarih is { } day ? IstanbulTime.StartOfDayUtc(day) : null,
            tarih is { } next ? IstanbulTime.StartOfDayUtc(next.AddDays(1)) : null,
            cancellationToken);

        var rows = new List<IReadOnlyList<string>>
        {
            cargo
                ? new[] { "Ad Soyad", "Telefon", "Adres", "İl", "İlçe", "Tutar", "Ödeme", "Sipariş No", "Kalemler" }
                : new[] { "Sipariş No", "Tarih", "Durum", "Kanal", "Ödeme", "Ad Soyad", "Telefon", "E-posta", "Adres", "İl", "İlçe",
                          "Ara Toplam", "Kargo", "Toplam", "Kargo Firması", "Takip No", "Fatura No", "Kalemler", "Not" }
        };
        foreach (var (order, items, _, _) in orders)
        {
            var lines = string.Join(" | ", items.Select(i => $"{i.ProductName} x{i.Quantity}{(i.IsGift ? " (hediye)" : "")}"));
            rows.Add(cargo
                ? new[] { order.FullName, order.Phone, order.Address, order.City, order.District, CsvFile.Money(order.Total),
                   PaymentLabels.Method(order.PaymentMethod), order.OrderNo, lines }
                : new[] { order.OrderNo, IstanbulTime.Format(order.CreatedAt), OrderLabels.For(order.Status), PaymentLabels.Source(order.Source),
                   PaymentLabels.Method(order.PaymentMethod), order.FullName, order.Phone, order.Email ?? "", order.Address, order.City,
                   order.District, CsvFile.Money(order.Subtotal), CsvFile.Money(order.ShippingFee), CsvFile.Money(order.Total),
                   order.Carrier ?? "", order.TrackingNo ?? "", order.InvoiceNo ?? "", lines, order.Note ?? "" });
        }

        var format = cargo ? "kargo" : "tam";
        await _auditService.WriteAsync(HttpContext, "sipariş dışa aktarma", "sipariş", null, $"{format} · {orders.Count} sipariş");
        return File(CsvFile.Build(rows), CsvFile.ContentType, $"siparisler-{format}-{_clock.GetUtcNow():yyyyMMdd}.csv");
    }

    [HttpGet("admin/orders/new")]
    public IActionResult New() => View(WithLines(new ManualOrderFormViewModel()));

    [HttpPost("admin/orders/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ManualOrderFormViewModel form, CancellationToken cancellationToken)
    {
        var (status, result) = await _orderService.PlaceManualAsync(new ManualOrderDraft(
            form.FullName,
            form.Phone,
            form.Email,
            form.Address,
            form.City,
            form.District,
            form.Note,
            form.PaymentMethod,
            form.Source,
            form.Lines.Select(l => new ManualOrderLine(l.Code ?? string.Empty, l.Quantity)).ToList(),
            form.ShippingFeeOverride,
            form.NotifyCustomer), cancellationToken);

        if (status != HttpStatusCode.Created)
        {
            Response.StatusCode = (int)status;
            form.ErrorMessage = result.Message;
            return View(WithLines(form));
        }

        var order = result.Data!;
        await _auditService.WriteAsync(HttpContext, "manuel sipariş", "sipariş", order.Id, PaymentLabels.Source(order.Source));
        return RedirectToAction(nameof(Detail), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var (status, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        // Detayı açmak siparişi okunmuş sayar; başlıktaki rozet buradan düşer.
        await _orderService.MarkSeenAsync(id, cancellationToken);
        return View(ViewFor(detail.Data!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        int id,
        OrderStatus next,
        string? carrier,
        string? trackingNo,
        CancellationToken cancellationToken)
    {
        var (status, result) = await _orderService.ChangeStatusAsync(id, next, carrier, trackingNo, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK)
        {
            return await DetailWithErrorAsync(id, status, result.Message, cancellationToken);
        }

        await _auditService.WriteAsync(HttpContext, "durum: " + OrderLabels.For(next), "sipariş", id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("admin/orders/{id:int}/duzenle")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var (status, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        var order = detail.Data!.Order;
        if (!OrderRules.CanEdit(order.Status))
        {
            return RedirectToAction(nameof(Detail), new { id });
        }

        var lines = detail.Data.Items.Where(i => !i.IsGift).ToList();
        return View(new OrderEditViewModel
        {
            Order = order,
            Address = order.Address,
            City = order.City,
            District = order.District,
            Phone = order.Phone,
            Note = order.Note,
            Lines = lines,
            Items = lines.Select(i => new OrderEditItemForm { Id = i.Id, Quantity = i.Quantity }).ToList()
        });
    }

    [HttpPost("admin/orders/{id:int}/duzenle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OrderEditViewModel form, CancellationToken cancellationToken)
    {
        var (status, result) = await _orderService.EditAsync(id, new OrderEdit(
            form.Address ?? string.Empty,
            form.City ?? string.Empty,
            form.District ?? string.Empty,
            form.Phone ?? string.Empty,
            form.Note,
            form.Items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.Last().Quantity)), cancellationToken);

        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK)
        {
            var (_, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
            Response.StatusCode = (int)status;
            form.Order = detail.Data!.Order;
            form.Lines = detail.Data.Items.Where(i => !i.IsGift).ToList();
            form.ErrorMessage = result.Message;
            return View(form);
        }

        await _auditService.WriteAsync(HttpContext, "düzenle", "sipariş", id, result.Data is { Length: > 0 } changes ? changes : null);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("admin/orders/{id:int}/fatura")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(PrivateFileStorage.MaxBytes + 64_000)]
    public async Task<IActionResult> Invoice(int id, string? invoiceNo, DateTime? invoiceDate, IFormFile? file, CancellationToken cancellationToken)
    {
        var bytes = await ReadAsync(file, cancellationToken);
        if (bytes is null || PrivateFileStorage.Kind(bytes) != "pdf" || invoiceDate is null)
        {
            return await DetailWithErrorAsync(id, HttpStatusCode.BadRequest,
                "Fatura numarası, tarihi ve en çok 5 MB'lık bir PDF gerekli.", cancellationToken);
        }

        var stored = await _files.SaveAsync("faturalar", "pdf", bytes, cancellationToken);
        var (status, result) = await _orderService.SetInvoiceAsync(id, invoiceNo ?? string.Empty, invoiceDate.Value, stored, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            _files.Delete(stored);
            return status == HttpStatusCode.NotFound
                ? NotFound()
                : await DetailWithErrorAsync(id, status, result.Message, cancellationToken);
        }

        _files.Delete(result.Data);
        await _auditService.WriteAsync(HttpContext, "fatura", "sipariş", id, invoiceNo!.Trim());
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("admin/orders/{id:int}/fatura")]
    public async Task<IActionResult> InvoiceFile(int id, CancellationToken cancellationToken)
    {
        var (status, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
        return status == HttpStatusCode.OK && _files.Resolve(detail.Data!.Order.InvoiceFile) is { } path
            ? PhysicalFile(path, "application/pdf", $"fatura-{detail.Data.Order.OrderNo}.pdf")
            : NotFound();
    }

    [HttpGet("admin/orders/{id:int}/dekont/{noticeId:int}")]
    public async Task<IActionResult> Receipt(int id, int noticeId, CancellationToken cancellationToken)
    {
        var (status, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
        var notice = status == HttpStatusCode.OK ? detail.Data!.Notices?.FirstOrDefault(n => n.Id == noticeId) : null;
        return _files.Resolve(notice?.ReceiptFile) is { } path
            ? PhysicalFile(path, PrivateFileStorage.ContentType(notice!.ReceiptFile!), $"dekont-{detail.Data!.Order.OrderNo}{Path.GetExtension(path)}")
            : NotFound();
    }

    [HttpPost("admin/orders/{id:int}/havale-onayla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveTransfer(int id, CancellationToken cancellationToken)
    {
        var (status, result) = await _orderService.ApprovePaymentAsync(id, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK)
        {
            return await DetailWithErrorAsync(id, status, result.Message, cancellationToken);
        }

        await _auditService.WriteAsync(HttpContext, "havale onayı", "sipariş", id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("admin/orders/{id:int}/iade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var (status, result) = await _paymentService.RefundAsync(id, ip, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK)
        {
            return await DetailWithErrorAsync(id, status, result.Message, cancellationToken);
        }

        await _auditService.WriteAsync(HttpContext, "iade", "sipariş", id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anonymize(int id, CancellationToken cancellationToken)
    {
        var (status, result) = await _orderService.AnonymizeAsync(id, cancellationToken);
        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (status != HttpStatusCode.OK)
        {
            return await DetailWithErrorAsync(id, status, result.Message, cancellationToken);
        }

        await _auditService.WriteAsync(HttpContext, "kişisel veri anonimleştirildi", "sipariş", id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task<IActionResult> DetailWithErrorAsync(int id, HttpStatusCode status, string message, CancellationToken cancellationToken)
    {
        var (found, detail) = await _orderService.GetByIdAsync(id, cancellationToken);
        if (found != HttpStatusCode.OK)
        {
            return NotFound();
        }

        Response.StatusCode = (int)status;
        return View("Detail", ViewFor(detail.Data!, message));
    }

    /// <summary>Dosya yoksa, boşsa ya da sınırı aşıyorsa null.</summary>
    private static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is 0 or > PrivateFileStorage.MaxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static ManualOrderFormViewModel WithLines(ManualOrderFormViewModel form)
    {
        while (form.Lines.Count < ManualOrderFormViewModel.LineCount)
        {
            form.Lines.Add(new ManualOrderLineForm { Quantity = 1 });
        }

        return form;
    }

    private OrderDetailViewModel ViewFor(OrderDetail detail, string? errorMessage = null) => new()
    {
        Detail = detail,
        ErrorMessage = errorMessage,
        Carriers = _shipping.Carriers.Select(c => c.Name).ToList(),
        TrackingUrl = _shipping.TrackingUrl(detail.Order.Carrier, detail.Order.TrackingNo)
    };
}
