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

public class ReturnManager : IReturnService
{
    private readonly IReturnRequestDal _returnDal;
    private readonly IReturnRequestItemDal _returnItemDal;
    private readonly IOrderDal _orderDal;
    private readonly IOrderItemDal _orderItemDal;
    private readonly IProductDal _productDal;
    private readonly IProductVariantDal _variantDal;
    private readonly IPaymentDal _paymentDal;
    private readonly IPaymentProvider _provider;
    private readonly IPaymentService _payments;
    private readonly IOrderService _orders;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ReturnManager(
        IReturnRequestDal returnDal,
        IReturnRequestItemDal returnItemDal,
        IOrderDal orderDal,
        IOrderItemDal orderItemDal,
        IProductDal productDal,
        IProductVariantDal variantDal,
        IPaymentDal paymentDal,
        IPaymentProvider provider,
        IPaymentService payments,
        IOrderService orders,
        INotificationService notifications,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _returnDal = returnDal;
        _returnItemDal = returnItemDal;
        _orderDal = orderDal;
        _orderItemDal = orderItemDal;
        _productDal = productDal;
        _variantDal = variantDal;
        _paymentDal = paymentDal;
        _provider = provider;
        _payments = payments;
        _orders = orders;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IDataResult<ReturnForm>)> GetFormAsync(string orderNo, Guid accessToken, CancellationToken cancellationToken = default)
    {
        var (status, message, order) = await RequestableOrderAsync(orderNo, accessToken, cancellationToken);
        if (order is null)
        {
            return (status, new ErrorDataResult<ReturnForm>(message));
        }

        var requested = await RequestedQuantitiesAsync(order.Id, cancellationToken);
        var lines = new List<ReturnFormLine>();
        foreach (var item in (await _orderItemDal.GetListAsync(i => i.OrderId == order.Id && !i.IsGift, cancellationToken)).OrderBy(i => i.Id))
        {
            var current = await _variantDal.GetAsync(v => v.Sku == item.Sku, cancellationToken);
            var alternatives = current is null
                ? []
                : await _variantDal.GetListAsync(v => v.ProductId == current.ProductId && v.Id != current.Id && v.Stock > 0, cancellationToken);
            lines.Add(new ReturnFormLine(item, Math.Max(item.Quantity - requested.GetValueOrDefault(item.Id), 0), alternatives.OrderBy(v => v.Id).ToList()));
        }

        return (HttpStatusCode.OK, new SuccessDataResult<ReturnForm>(new ReturnForm(order, lines, order.PaymentMethod != PaymentMethod.KrediKarti)));
    }

    public async Task<(HttpStatusCode, IDataResult<ReturnRequest>)> RequestAsync(
        string orderNo,
        Guid accessToken,
        ReturnDraft draft,
        CancellationToken cancellationToken = default)
    {
        var (status, message, order) = await RequestableOrderAsync(orderNo, accessToken, cancellationToken);
        if (order is null)
        {
            return (status, new ErrorDataResult<ReturnRequest>(message));
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        var reason = draft.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 1000)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<ReturnRequest>("Talebin nedenini yazın (en çok 1000 karakter)."));
        }

        var lines = draft.Lines.Where(l => l.Quantity > 0).ToList();
        if (lines.Count == 0 || lines.GroupBy(l => l.OrderItemId).Any(g => g.Count() > 1))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<ReturnRequest>("En az bir ürün ve adet seçin."));
        }

        var items = (await _orderItemDal.GetListAsync(i => i.OrderId == order.Id && !i.IsGift, cancellationToken)).ToDictionary(i => i.Id);
        var requested = await RequestedQuantitiesAsync(order.Id, cancellationToken);
        foreach (var line in lines)
        {
            if (!items.TryGetValue(line.OrderItemId, out var item))
            {
                return (HttpStatusCode.BadRequest, new ErrorDataResult<ReturnRequest>("Seçilen ürün bu siparişte yok."));
            }

            var left = item.Quantity - requested.GetValueOrDefault(item.Id);
            if (line.Quantity > left)
            {
                return (HttpStatusCode.BadRequest, new ErrorDataResult<ReturnRequest>(
                    $"{item.ProductName} için en çok {Math.Max(left, 0)} adet talep edilebilir."));
            }
        }

        string? iban = null;
        if (draft.Type == ReturnType.Iade && order.PaymentMethod != PaymentMethod.KrediKarti)
        {
            if (!IbanRules.TryNormalize(draft.Iban, out var normalized))
            {
                return (HttpStatusCode.BadRequest, new ErrorDataResult<ReturnRequest>("Geri ödeme için geçerli bir IBAN yazın (TR ile başlayan 26 karakter)."));
            }

            iban = normalized;
        }

        if (draft.Type == ReturnType.Degisim)
        {
            foreach (var line in lines)
            {
                var current = await _variantDal.GetAsync(v => v.Sku == items[line.OrderItemId].Sku, cancellationToken);
                var wanted = (line.NewSku?.Trim()) is { Length: > 0 } sku ? await _variantDal.GetAsync(v => v.Sku == sku, cancellationToken) : null;
                // Varyantlar ürünün fiyatını paylaşır: aynı üründe kaldıkça fark yoktur, değişim ücretsizdir.
                if (current is null || wanted is null || wanted.ProductId != current.ProductId || wanted.Id == current.Id)
                {
                    return (HttpStatusCode.BadRequest, new ErrorDataResult<ReturnRequest>("Değişimde aynı ürünün başka bir bedeni ya da rengi seçilir."));
                }

                if (wanted.Stock < line.Quantity)
                {
                    return (HttpStatusCode.Conflict, new ErrorDataResult<ReturnRequest>("Seçtiğiniz beden/renk şu an stokta yok; iade talebi açabilirsiniz."));
                }
            }
        }

        var request = new ReturnRequest
        {
            OrderId = order.Id,
            Type = draft.Type,
            Status = ReturnStatus.Bekliyor,
            Reason = reason,
            PhotoFile = draft.PhotoFile,
            RefundIban = iban,
            CreatedAt = now
        };

        await _unitOfWork.InTransactionAsync(async () =>
        {
            await _returnDal.AddAsync(request, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            foreach (var line in lines)
            {
                await _returnItemDal.AddAsync(new ReturnRequestItem
                {
                    ReturnRequestId = request.Id,
                    OrderItemId = line.OrderItemId,
                    Quantity = line.Quantity,
                    NewSku = draft.Type == ReturnType.Degisim ? line.NewSku!.Trim() : null
                }, cancellationToken);
            }

            return await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return (HttpStatusCode.Created, new SuccessDataResult<ReturnRequest>(request, "Talebiniz alındı; inceleyip e-postayla dönüş yapacağız."));
    }

    public async Task<(HttpStatusCode, IResult)> ApproveAsync(int id, CancellationToken cancellationToken = default)
        => await DecideAsync(id, ReturnStatus.Onaylandi, null, "Talep onaylandı; müşteriye iade adresi gidecek.", cancellationToken);

    public async Task<(HttpStatusCode, IResult)> RejectAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        var text = reason?.Trim() ?? string.Empty;
        if (text.Length is 0 or > 500)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Ret gerekçesi zorunlu (en çok 500 karakter)."));
        }

        return await DecideAsync(id, ReturnStatus.Reddedildi, text, "Talep reddedildi; gerekçe müşteriye gidecek.", cancellationToken);
    }

    public async Task<(HttpStatusCode, IResult)> ReceiveAsync(int id, string ip, CancellationToken cancellationToken = default)
    {
        var detail = await DetailAsync(id, cancellationToken);
        if (detail is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Talep bulunamadı."));
        }

        var (request, order, lines) = detail;
        if (request.Status != ReturnStatus.Onaylandi)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Yalnız onaylanmış talebin ürünü teslim alınır."));
        }

        var payment = order.PaymentMethod == PaymentMethod.KrediKarti
            ? (await _paymentDal.GetListAsync(p => p.OrderId == order.Id, cancellationToken)).MaxBy(p => p.Id)
            : null;
        var byCard = request.Type == ReturnType.Iade && payment is { Status: PaymentStatus.Basarili, PaymentId: not null };
        if (request.Type == ReturnType.Iade && order.PaymentMethod == PaymentMethod.KrediKarti && !byCard)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Bu siparişin iade edilecek kart ödemesi yok (zaten iade edilmiş olabilir)."));
        }

        var amount = request.Type == ReturnType.Iade ? await RefundAmountAsync(request, order, lines, cancellationToken) : 0m;
        var refunded = byCard ? await CardRefundedAsync(order.Id, request.Id, cancellationToken) : 0m;
        amount = byCard ? Math.Min(amount, payment!.Amount - refunded) : amount;
        var now = _clock.GetUtcNow().UtcDateTime;

        try
        {
            await _unitOfWork.InTransactionAsync(async () =>
            {
                // Koşullu UPDATE satırı kilitler: eşzamanlı ikinci teslim alma 0 alır, stok ve para ikinci kez dönmez.
                if (await _returnDal.TryMoveAsync(request.Id, ReturnStatus.Onaylandi, ReturnStatus.TeslimAlindi, cancellationToken) == 0)
                {
                    throw new ReturnConflictException("Bu talep zaten teslim alındı.");
                }

                foreach (var (item, orderItem) in lines)
                {
                    await ReturnStockAsync(orderItem.Sku, item.Quantity, cancellationToken);
                    if (request.Type == ReturnType.Degisim
                        && (await _variantDal.GetAsync(v => v.Sku == item.NewSku, cancellationToken) is not { } wanted
                            || await _variantDal.TryDecrementStockAsync(wanted.Id, item.Quantity, cancellationToken) == 0))
                    {
                        throw new ReturnConflictException("Değişim için seçilen beden/renk stokta kalmadı; müşteriyle iade konuşun.");
                    }
                }

                if (byCard)
                {
                    // Ödemenin tamamı geri dönüyorsa kayıt da iade durumuna geçer; aynı gün ve ilk iadeyse sağlayıcı iptal dener.
                    var full = refunded + amount >= payment!.Amount;
                    if (full && await _paymentDal.TryRefundAsync(payment.Id, now, cancellationToken) == 0)
                    {
                        throw new ReturnConflictException("Bu ödeme zaten iade edildi.");
                    }

                    var refund = await _provider.RefundAsync(
                        new PaymentRefundRequest(payment.PaymentId!, payment.ConversationId, amount, ip, Partial: !(full && refunded == 0m)),
                        cancellationToken);
                    if (!refund.Success)
                    {
                        throw new ReturnGatewayException("İade yapılamadı: " + (refund.ErrorMessage ?? "sağlayıcı yanıt vermedi."));
                    }
                }

                // İzlenen kayda yalnız istisna noktalarından sonra dokunulur: geri alınan işlemde bağlamda kirli değişiklik kalmaz.
                var tracked = (await _returnDal.GetTrackedAsync(r => r.Id == request.Id, cancellationToken))!;
                tracked.ReceivedAt = now;
                tracked.RefundAmount = amount;
                tracked.RefundedAt = byCard ? now : null;
                tracked.Status = byCard || request.Type == ReturnType.Degisim ? ReturnStatus.Tamamlandi : ReturnStatus.TeslimAlindi;

                return await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (ReturnConflictException conflict)
        {
            return (HttpStatusCode.Conflict, new ErrorResult(conflict.Message));
        }
        catch (ReturnGatewayException gateway)
        {
            return (HttpStatusCode.BadGateway, new ErrorResult(gateway.Message));
        }

        return (HttpStatusCode.OK, new SuccessResult(byCard
            ? "Ürün teslim alındı, stok iade edildi, kart iadesi yapıldı."
            : request.Type == ReturnType.Degisim
                ? "Ürün teslim alındı; yeni ürünün stoğu düştü, kargoya verin."
                : "Ürün teslim alındı, stok iade edildi; IBAN'a geri ödemeyi yapıp işaretleyin."));
    }

    public async Task<(HttpStatusCode, IResult)> MarkRefundedAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await _returnDal.GetTrackedAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Talep bulunamadı."));
        }

        if (request.Type != ReturnType.Iade || request.Status != ReturnStatus.TeslimAlindi || request.RefundedAt is not null)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Yalnız teslim alınmış, geri ödemesi bekleyen iade işaretlenir."));
        }

        request.Status = ReturnStatus.Tamamlandi;
        request.RefundedAt = _clock.GetUtcNow().UtcDateTime;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Geri ödeme yapıldı olarak işaretlendi."));
    }

    public async Task<(HttpStatusCode, IDataResult<List<ReturnListItem>>)> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _returnDal.GetListAsync(cancellationToken: cancellationToken);
        var orderIds = requests.Select(r => r.OrderId).Distinct().ToList();
        var orders = orderIds.Count == 0
            ? new Dictionary<int, Order>()
            : (await _orderDal.GetListAsync(o => orderIds.Contains(o.Id), cancellationToken)).ToDictionary(o => o.Id);
        var now = _clock.GetUtcNow().UtcDateTime;
        return (HttpStatusCode.OK, new SuccessDataResult<List<ReturnListItem>>(requests
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Select(r => new ReturnListItem(r, orders[r.OrderId].OrderNo, orders[r.OrderId].FullName, ReturnRules.RefundDaysLeft(r, now)))
            .ToList()));
    }

    public async Task<(HttpStatusCode, IDataResult<ReturnDetail>)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await DetailAsync(id, cancellationToken) is { } detail
            ? (HttpStatusCode.OK, new SuccessDataResult<ReturnDetail>(detail))
            : (HttpStatusCode.NotFound, new ErrorDataResult<ReturnDetail>("Talep bulunamadı."));

    public async Task<List<ReturnDetail>> GetForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var details = new List<ReturnDetail>();
        foreach (var request in (await _returnDal.GetListAsync(r => r.OrderId == orderId, cancellationToken)).OrderBy(r => r.Id))
        {
            details.Add((await DetailAsync(request.Id, cancellationToken))!);
        }

        return details;
    }

    public async Task<(HttpStatusCode, IResult)> CancelByCustomerAsync(
        string orderNo,
        Guid accessToken,
        string? iban,
        string ip,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetAsync(o => o.OrderNo == orderNo && o.AccessToken == accessToken, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sipariş bulunamadı."));
        }

        if (!ReturnRules.CanCustomerCancel(order.Status))
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Hazırlanmaya başlamış sipariş iptal edilemez; teslimden sonra iade talebi açabilirsiniz."));
        }

        if (order.PaymentMethod == PaymentMethod.KrediKarti)
        {
            var payment = (await _paymentDal.GetListAsync(p => p.OrderId == order.Id, cancellationToken)).MaxBy(p => p.Id);
            // Çekilmiş ödeme iadeyle (sipariş iptal, stok geri, tek işlemde); çekilmemişte açık ödeme kaydı iptalle kapanır.
            var (status, result) = payment?.Status == PaymentStatus.Basarili
                ? await _payments.RefundAsync(order.Id, ip, cancellationToken)
                : await _orders.ChangeStatusAsync(order.Id, OrderStatus.IptalEdildi, cancellationToken: cancellationToken);
            return status == HttpStatusCode.OK ? (status, new SuccessResult("Siparişiniz iptal edildi.")) : (status, result);
        }

        // Onaylı havalede para hesaba geçmiştir: IBAN'a elle geri ödenir.
        var paid = order.PaymentMethod == PaymentMethod.HavaleEft && order.Status == OrderStatus.Onaylandi;
        string? refundIban = null;
        if (paid && !IbanRules.TryNormalize(iban, out refundIban))
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Ödemenizin iadesi için geçerli bir IBAN yazın (TR ile başlayan 26 karakter)."));
        }

        var cancelled = await _unitOfWork.InTransactionAsync(async () =>
        {
            if (await _orderDal.TryChangeStatusAsync(order.Id, order.Status, OrderStatus.IptalEdildi, cancellationToken) == 0)
            {
                return false;
            }

            foreach (var item in await _orderItemDal.GetListAsync(i => i.OrderId == order.Id, cancellationToken))
            {
                await ReturnStockAsync(item.Sku, item.Quantity, cancellationToken);
            }

            if (paid)
            {
                var tracked = (await _orderDal.GetTrackedAsync(o => o.Id == order.Id, cancellationToken))!;
                tracked.RefundDue = order.Total;
                tracked.RefundIban = refundIban;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }, cancellationToken);

        return cancelled
            ? (HttpStatusCode.OK, new SuccessResult(paid
                ? "Siparişiniz iptal edildi; ödemeniz en geç 14 gün içinde IBAN'ınıza iade edilecek."
                : "Siparişiniz iptal edildi."))
            : (HttpStatusCode.Conflict, new ErrorResult("Sipariş durumu değişti; sayfayı yenileyin."));
    }

    public async Task<(HttpStatusCode, IResult)> MarkOrderRefundedAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sipariş bulunamadı."));
        }

        if (order.RefundDue is null || order.RefundedAt is not null)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Bu siparişte bekleyen elle geri ödeme yok."));
        }

        order.RefundedAt = _clock.GetUtcNow().UtcDateTime;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Geri ödeme yapıldı olarak işaretlendi."));
    }

    private async Task<(HttpStatusCode, IResult)> DecideAsync(
        int id,
        ReturnStatus next,
        string? rejectReason,
        string message,
        CancellationToken cancellationToken)
    {
        var request = await _returnDal.GetAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Talep bulunamadı."));
        }

        var order = (await _orderDal.GetAsync(o => o.Id == request.OrderId, cancellationToken))!;
        var decided = await _unitOfWork.InTransactionAsync(async () =>
        {
            if (await _returnDal.TryMoveAsync(id, ReturnStatus.Bekliyor, next, cancellationToken) == 0)
            {
                return false;
            }

            var tracked = (await _returnDal.GetTrackedAsync(r => r.Id == id, cancellationToken))!;
            tracked.Status = next;
            tracked.DecidedAt = _clock.GetUtcNow().UtcDateTime;
            tracked.RejectReason = rejectReason;
            await (next == ReturnStatus.Onaylandi
                ? _notifications.QueueReturnApprovedAsync(order, tracked, cancellationToken)
                : _notifications.QueueReturnRejectedAsync(order, tracked, cancellationToken));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return decided
            ? (HttpStatusCode.OK, new SuccessResult(message))
            : (HttpStatusCode.Conflict, new ErrorResult("Yalnız bekleyen talep onaylanır ya da reddedilir."));
    }

    private async Task<ReturnDetail?> DetailAsync(int id, CancellationToken cancellationToken)
    {
        var request = await _returnDal.GetAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        var order = (await _orderDal.GetAsync(o => o.Id == request.OrderId, cancellationToken))!;
        var items = await _returnItemDal.GetListAsync(i => i.ReturnRequestId == id, cancellationToken);
        var orderItems = (await _orderItemDal.GetListAsync(i => i.OrderId == order.Id, cancellationToken)).ToDictionary(i => i.Id);
        return new ReturnDetail(request, order, items.OrderBy(i => i.Id).Select(i => (i, orderItems[i.OrderItemId])).ToList());
    }

    /// <summary>Anahtarı tutan, teslim edilmiş ve 14 günü dolmamış sipariş; değilse durum kodu ve mesaj.</summary>
    private async Task<(HttpStatusCode Status, string Message, Order? Order)> RequestableOrderAsync(
        string orderNo,
        Guid accessToken,
        CancellationToken cancellationToken)
    {
        var order = await _orderDal.GetAsync(o => o.OrderNo == orderNo && o.AccessToken == accessToken, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, "Sipariş bulunamadı.", null);
        }

        if (order.Status != OrderStatus.TeslimEdildi)
        {
            return (HttpStatusCode.Conflict, "İade ve değişim talebi teslim edilmiş siparişte açılır.", null);
        }

        return ReturnRules.CanRequest(order, _clock.GetUtcNow().UtcDateTime)
            ? (HttpStatusCode.OK, string.Empty, order)
            : (HttpStatusCode.BadRequest, "İade ve değişim talebi teslimden itibaren 14 gün içinde açılır; bu sipariş için süre doldu.", null);
    }

    /// <summary>Reddedilmemiş taleplerde kalem başına istenmiş adet.</summary>
    private async Task<Dictionary<int, int>> RequestedQuantitiesAsync(int orderId, CancellationToken cancellationToken)
    {
        var open = (await _returnDal.GetListAsync(r => r.OrderId == orderId && r.Status != ReturnStatus.Reddedildi, cancellationToken))
            .Select(r => r.Id)
            .ToList();
        return open.Count == 0
            ? []
            : (await _returnItemDal.GetListAsync(i => open.Contains(i.ReturnRequestId), cancellationToken))
                .GroupBy(i => i.OrderItemId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
    }

    /// <summary>Kalemlerin satır tutarı; bu iadeyle siparişin tüm ürün tutarı geri dönmüş oluyorsa kargo bedeli de eklenir
    /// (cayma halinde teslim masrafı da iade edilir, Yönetmelik m. 13).</summary>
    private async Task<decimal> RefundAmountAsync(
        ReturnRequest request,
        Order order,
        IReadOnlyList<(ReturnRequestItem Item, OrderItem OrderItem)> lines,
        CancellationToken cancellationToken)
    {
        var amount = lines.Sum(l => l.OrderItem.UnitPrice * l.Item.Quantity);
        var before = (await _returnDal.GetListAsync(
                r => r.OrderId == order.Id && r.Id != request.Id && r.Type == ReturnType.Iade && r.ReceivedAt != null,
                cancellationToken))
            .Sum(r => r.RefundAmount);
        return before + amount >= order.Subtotal ? amount + order.ShippingFee : amount;
    }

    private async Task<decimal> CardRefundedAsync(int orderId, int exceptId, CancellationToken cancellationToken)
        => (await _returnDal.GetListAsync(r => r.OrderId == orderId && r.Id != exceptId && r.RefundedAt != null, cancellationToken))
            .Sum(r => r.RefundAmount);

    /// <summary>Stok kodu varyanta uymuyorsa satır varyantsız (Ev) üründür; iade ürünün stoğuna yazılır.</summary>
    private async Task ReturnStockAsync(string sku, int quantity, CancellationToken cancellationToken)
    {
        if (await _variantDal.IncrementStockBySkuAsync(sku, quantity, cancellationToken) == 0)
        {
            await _productDal.IncrementStockBySlugAsync(sku, quantity, cancellationToken);
        }
    }

    /// <summary>İşlemi geri almak için; dışarı sızmaz, 409'a çevrilir.</summary>
    private sealed class ReturnConflictException(string message) : Exception(message);

    /// <summary>Sağlayıcı iadeyi reddetti; işlem geri alınır, 502'ye çevrilir.</summary>
    private sealed class ReturnGatewayException(string message) : Exception(message);
}
