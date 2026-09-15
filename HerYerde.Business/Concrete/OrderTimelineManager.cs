using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Concrete;

public class OrderTimelineManager : IOrderTimelineService
{
    public const int NoteMaxLength = 1000;

    /// <summary>Aynı andaki satırların sırası: sipariş, postası, işlem, not, iade.</summary>
    private static readonly string[] KindOrder = ["siparis", "posta", "islem", "not", "iade"];

    private readonly IOrderDal _orderDal;
    private readonly IOrderNoteDal _noteDal;
    private readonly IAdminAuditLogDal _auditDal;
    private readonly IOutboxMessageDal _outboxDal;
    private readonly IReturnRequestDal _returnDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public OrderTimelineManager(
        IOrderDal orderDal,
        IOrderNoteDal noteDal,
        IAdminAuditLogDal auditDal,
        IOutboxMessageDal outboxDal,
        IReturnRequestDal returnDal,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _orderDal = orderDal;
        _noteDal = noteDal;
        _auditDal = auditDal;
        _outboxDal = outboxDal;
        _returnDal = returnDal;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IResult)> AddNoteAsync(int orderId, int adminId, string text, CancellationToken cancellationToken = default)
    {
        var note = text?.Trim() ?? string.Empty;
        if (note.Length is 0 or > NoteMaxLength)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult($"Not boş olamaz (en çok {NoteMaxLength} karakter)."));
        }

        if (await _orderDal.GetAsync(o => o.Id == orderId, cancellationToken) is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sipariş bulunamadı."));
        }

        await _noteDal.AddAsync(new OrderNote { OrderId = orderId, AdminId = adminId, Text = note, CreatedAt = _clock.GetUtcNow().UtcDateTime }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Not eklendi."));
    }

    public async Task<List<TimelineEntry>> GetAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return [];
        }

        var entries = new List<TimelineEntry>
        {
            new(order.CreatedAt, "siparis", "Sipariş alındı",
                $"{PaymentLabels.Source(order.Source)} · {PaymentLabels.Method(order.PaymentMethod)}",
                order.Source == OrderSource.Site ? "Müşteri" : "Yönetim")
        };

        // Sipariş postalarının konusu " · {sipariş no}" ile biter (NotificationManager.ForgetOrderAsync ile aynı eşleşme).
        var suffix = " · " + order.OrderNo;
        entries.AddRange((await _outboxDal.GetListAsync(m => m.Subject.EndsWith(suffix), cancellationToken))
            .Select(m => new TimelineEntry(m.CreatedAt, "posta", m.Subject,
                m.Status == OutboxStatus.Gonderildi ? "gönderildi" : m.Status == OutboxStatus.Basarisiz ? "gönderilemedi" : "kuyrukta",
                null)));

        // İç not kendi satırıyla görünür; izdeki "iç not" kaydı çift yazılmaz.
        var audit = await _auditDal.GetForEntityAsync("sipariş", orderId, cancellationToken);
        entries.AddRange(audit
            .Where(row => row.Log.Action != "iç not")
            .Select(row => new TimelineEntry(row.Log.At, "islem", row.Log.Action, row.Log.Detail,
                row.Log.AdminId == 0 ? "Müşteri" : row.AdminEmail ?? "#" + row.Log.AdminId)));

        // Notu yazanın e-postası izdeki yönetici satırlarından; iz temizlenmişse kimlik gösterilir.
        var emails = audit.Where(row => row.AdminEmail is not null).GroupBy(row => row.Log.AdminId).ToDictionary(g => g.Key, g => g.First().AdminEmail!);
        entries.AddRange((await _noteDal.GetListAsync(n => n.OrderId == orderId, cancellationToken))
            .Select(n => new TimelineEntry(n.CreatedAt, "not", "İç not", n.Text, emails.GetValueOrDefault(n.AdminId, "#" + n.AdminId))));

        foreach (var request in await _returnDal.GetListAsync(r => r.OrderId == orderId, cancellationToken))
        {
            var kind = ReturnLabels.Type(request.Type);
            entries.Add(new TimelineEntry(request.CreatedAt, "iade", $"{kind} talebi açıldı", null, "Müşteri"));
            if (request.DecidedAt is { } decided)
            {
                entries.Add(new TimelineEntry(decided, "iade", request.Status == ReturnStatus.Reddedildi ? $"{kind} talebi reddedildi" : $"{kind} talebi onaylandı", request.RejectReason, null));
            }

            if (request.ReceivedAt is { } received)
            {
                entries.Add(new TimelineEntry(received, "iade", "İade ürünü teslim alındı", null, null));
            }

            if (request.RefundedAt is { } refunded)
            {
                entries.Add(new TimelineEntry(refunded, "iade", "Geri ödeme yapıldı", null, null));
            }
        }

        return entries
            .OrderBy(e => e.At)
            .ThenBy(e => Array.IndexOf(KindOrder, e.Kind))
            .ToList();
    }
}
