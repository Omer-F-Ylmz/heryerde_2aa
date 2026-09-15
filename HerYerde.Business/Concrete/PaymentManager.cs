using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.Extensions.Options;

namespace HerYerde.Business.Concrete;

public class PaymentManager : IPaymentService
{
    public const string Provider = "iyzico";

    /// <summary>Saklanan sağlayıcı yanıtının üst sınırı (payment.raw_response).</summary>
    public const int RawResponseLimit = 4000;

    private const string Declined = "Ödemeniz onaylanmadı; kartınızdan çekim yapılmadı.";

    private readonly IPaymentDal _paymentDal;
    private readonly IOrderDal _orderDal;
    private readonly IOrderItemDal _orderItemDal;
    private readonly ICartItemDal _cartItemDal;
    private readonly IProductDal _productDal;
    private readonly IProductVariantDal _variantDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;
    private readonly IPaymentProvider _provider;
    private readonly ShopSettings _shop;
    private readonly TimeProvider _clock;

    public PaymentManager(
        IPaymentDal paymentDal,
        IOrderDal orderDal,
        IOrderItemDal orderItemDal,
        ICartItemDal cartItemDal,
        IProductDal productDal,
        IProductVariantDal variantDal,
        IUnitOfWork unitOfWork,
        INotificationService notifications,
        IPaymentProvider provider,
        IOptions<ShopSettings> shop,
        TimeProvider clock)
    {
        _paymentDal = paymentDal;
        _orderDal = orderDal;
        _orderItemDal = orderItemDal;
        _cartItemDal = cartItemDal;
        _productDal = productDal;
        _variantDal = variantDal;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _provider = provider;
        _shop = shop.Value;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IDataResult<ThreeDsForm>)> StartAsync(
        int orderId,
        PaymentCard card,
        string buyerIp,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentDal.GetTrackedAsync(
            p => p.OrderId == orderId && p.Status == PaymentStatus.Baslatildi,
            cancellationToken);
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (payment is null || order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<ThreeDsForm>("Ödeme kaydı bulunamadı."));
        }

        var items = await _orderItemDal.GetListAsync(i => i.OrderId == orderId, cancellationToken);
        // Dönüş adresi istek başlığından değil yapılandırmadan kurulur: Host başlığıyla başka alana yönlendirilemez.
        var result = await _provider.InitThreeDsAsync(
            new PaymentInitRequest(order, items, payment.ConversationId, card, buyerIp, _shop.BaseUrl + "/odeme/3d-donus"),
            cancellationToken);

        payment.PaymentId = result.PaymentId;
        payment.RawResponse = MaskRaw(result.RawResponse);
        payment.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

        if (result is { Success: true, Form: { } form })
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return (HttpStatusCode.OK, new SuccessDataResult<ThreeDsForm>(form));
        }

        payment.Status = PaymentStatus.Basarisiz;
        order.Status = OrderStatus.IptalEdildi;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.PaymentRequired, new ErrorDataResult<ThreeDsForm>(
            "Ödeme başlatılamadı: " + (result.ErrorMessage ?? "sağlayıcı yanıt vermedi.")));
    }

    public async Task<(HttpStatusCode, IDataResult<Order>)> CompleteAsync(
        PaymentCallback callback,
        CancellationToken cancellationToken = default)
    {
        var payment = string.IsNullOrEmpty(callback.ConversationId)
            ? null
            : await _paymentDal.GetTrackedAsync(p => p.ConversationId == callback.ConversationId, cancellationToken);
        var order = payment is null ? null : await _orderDal.GetTrackedAsync(o => o.Id == payment.OrderId, cancellationToken);
        if (payment is null || order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<Order>("Ödeme kaydı bulunamadı."));
        }

        if (payment.Status != PaymentStatus.Baslatildi)
        {
            return Outcome(payment.Status, order);
        }

        string? failure = null;
        string? raw = null;
        if (!_provider.IsValidCallback(callback))
        {
            failure = "Ödeme doğrulanamadı; kartınızdan çekim yapılmadı.";
        }
        else if (callback.Status != "success" || callback.MdStatus != "1")
        {
            failure = Declined;
        }
        else
        {
            try
            {
                await _unitOfWork.InTransactionAsync(() => CaptureAsync(payment, order, callback, cancellationToken), cancellationToken);
                return (HttpStatusCode.OK, new SuccessDataResult<Order>(order, "Ödemeniz alındı."));
            }
            catch (AlreadyClosedException)
            {
                var current = (await _paymentDal.GetAsync(p => p.Id == payment.Id, cancellationToken))!;
                return Outcome(current.Status, order);
            }
            catch (CaptureRejectedException rejected)
            {
                failure = rejected.Message;
                raw = rejected.Raw;
            }
        }

        var moment = _clock.GetUtcNow().UtcDateTime;
        var closed = await _unitOfWork.InTransactionAsync(async () =>
        {
            if (await _paymentDal.TryCloseAsync(payment.Id, PaymentStatus.Basarisiz, moment, cancellationToken) == 0)
            {
                return false;
            }

            // Stok hiç düşmediği için iade yok; sipariş doğrudan iptal.
            payment.Status = PaymentStatus.Basarisiz;
            payment.UpdatedAt = moment;
            payment.RawResponse = raw ?? payment.RawResponse;
            order.Status = OrderStatus.IptalEdildi;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return closed
            ? (HttpStatusCode.PaymentRequired, new ErrorDataResult<Order>(failure))
            : Outcome((await _paymentDal.GetAsync(p => p.Id == payment.Id, cancellationToken))!.Status, order);
    }

    public async Task<(HttpStatusCode, IResult)> RefundAsync(int orderId, string ip, CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sipariş bulunamadı."));
        }

        var payment = (await _paymentDal.GetListAsync(p => p.OrderId == orderId, cancellationToken)).MaxBy(p => p.Id);
        if (payment?.Status != PaymentStatus.Basarili || payment.PaymentId is null)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("İade edilecek kart ödemesi yok (zaten iade edilmiş olabilir)."));
        }

        if (!OrderRules.CanRefund(order.Status))
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Kargoya verilmiş siparişte kart iadesi ürün geri gelince yapılır."));
        }

        try
        {
            await _unitOfWork.InTransactionAsync(async () =>
            {
                // Koşullu UPDATE satırı kilitler: eşzamanlı ikinci iade burada 0 alır ve sağlayıcıya hiç gitmez.
                var moment = _clock.GetUtcNow().UtcDateTime;
                if (await _paymentDal.TryRefundAsync(payment.Id, moment, cancellationToken) == 0)
                {
                    throw new AlreadyClosedException();
                }

                var refund = await _provider.RefundAsync(
                    new PaymentRefundRequest(payment.PaymentId, payment.ConversationId, payment.Amount, ip),
                    cancellationToken);
                if (!refund.Success)
                {
                    throw new CaptureRejectedException("İade yapılamadı: " + (refund.ErrorMessage ?? "sağlayıcı yanıt vermedi."), null);
                }

                foreach (var item in await _orderItemDal.GetListAsync(i => i.OrderId == order.Id, cancellationToken))
                {
                    if (await _variantDal.IncrementStockBySkuAsync(item.Sku, item.Quantity, cancellationToken) == 0)
                    {
                        await _productDal.IncrementStockBySlugAsync(item.Sku, item.Quantity, cancellationToken);
                    }
                }

                var tracked = (await _paymentDal.GetTrackedAsync(p => p.Id == payment.Id, cancellationToken))!;
                tracked.RawResponse = MaskRaw(refund.RawResponse) ?? tracked.RawResponse;
                order.Status = OrderStatus.IptalEdildi;
                return await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (AlreadyClosedException)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Bu ödeme zaten iade edildi."));
        }
        catch (CaptureRejectedException rejected)
        {
            return (HttpStatusCode.BadGateway, new ErrorResult(rejected.Message));
        }

        return (HttpStatusCode.OK, new SuccessResult("Ödeme iade edildi, sipariş iptal edildi, stok geri alındı."));
    }

    /// <summary>İşlem içinde: önce kayıt kilitlenip kapatılır (ikinci dönüş burada 0 alır), stok düşer, sonra çekim.
    /// Çekim reddedilir ya da tutar tutmazsa istisna işlemi geri alır; stok da kayıt da eski haline döner.
    /// İzlenen varlıklara yalnız istisna noktalarından sonra dokunulur.</summary>
    private async Task<int> CaptureAsync(Payment payment, Order order, PaymentCallback callback, CancellationToken cancellationToken)
    {
        var moment = _clock.GetUtcNow().UtcDateTime;
        if (await _paymentDal.TryCloseAsync(payment.Id, PaymentStatus.Basarili, moment, cancellationToken) == 0)
        {
            throw new AlreadyClosedException();
        }

        var items = await _orderItemDal.GetListAsync(i => i.OrderId == order.Id, cancellationToken);
        var shortages = new List<string>();
        var missingGifts = new List<OrderItem>();
        foreach (var item in items)
        {
            if (await TryDecrementAsync(item, cancellationToken))
            {
                continue;
            }

            if (item.IsGift)
            {
                missingGifts.Add(item);
            }
            else
            {
                shortages.Add(item.ProductName);
            }
        }

        if (shortages.Count > 0)
        {
            throw new CaptureRejectedException(
                $"Stok tükendi: {string.Join(", ", shortages.Distinct())}. Kartınızdan çekim yapılmadı.", null);
        }

        var auth = await _provider.CompleteThreeDsAsync(callback, cancellationToken);
        var raw = MaskRaw(auth.RawResponse);
        if (!auth.Success)
        {
            throw new CaptureRejectedException(Declined, raw);
        }

        if (auth.PaidPrice != order.Total)
        {
            throw new CaptureRejectedException("Ödeme tutarı sipariş tutarıyla uyuşmadı; sipariş iptal edildi.", raw);
        }

        payment.Status = PaymentStatus.Basarili;
        payment.PaymentId = auth.PaymentId ?? payment.PaymentId;
        payment.RawResponse = raw;
        payment.UpdatedAt = moment;

        // Son anda tükenen hediye satırı düşer; hediye yoksa ödeme yine geçer (sipariş anındaki kuralla aynı).
        foreach (var gift in missingGifts)
        {
            _orderItemDal.Delete((await _orderItemDal.GetTrackedAsync(i => i.Id == gift.Id, cancellationToken))!);
        }

        if (payment.CartId is { } cartId)
        {
            foreach (var line in await _cartItemDal.GetListAsync(i => i.CartId == cartId, cancellationToken))
            {
                _cartItemDal.Delete((await _cartItemDal.GetTrackedAsync(i => i.Id == line.Id, cancellationToken))!);
            }
        }

        var placed = items.Where(i => !missingGifts.Contains(i)).ToList();
        await _notifications.QueueOrderPlacedAsync(order, placed, store: false, cancellationToken: cancellationToken);
        return await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Satırın stok kodu varyantsa varyanttan, değilse ürünün kendisinden düşer; stok tutmayan üründe düşüm yok.</summary>
    private async Task<bool> TryDecrementAsync(OrderItem item, CancellationToken cancellationToken)
    {
        if (await _variantDal.GetAsync(v => v.Sku == item.Sku, cancellationToken) is { } variant)
        {
            return await _variantDal.TryDecrementStockAsync(variant.Id, item.Quantity, cancellationToken) > 0;
        }

        var product = await _productDal.GetAsync(p => p.Slug == item.Sku, cancellationToken);
        return product?.Stock is null
            || await _productDal.TryDecrementStockAsync(product.Id, item.Quantity, cancellationToken) > 0;
    }

    private static (HttpStatusCode, IDataResult<Order>) Outcome(PaymentStatus status, Order order)
        => status == PaymentStatus.Basarili
            ? (HttpStatusCode.OK, new SuccessDataResult<Order>(order, "Ödemeniz alındı."))
            : (HttpStatusCode.PaymentRequired, new ErrorDataResult<Order>(Declined));

    /// <summary>Yalnız kart alanları maskelenir (numara ve BIN son 4 hane hariç, cvc, kart sahibi); basketId, systemTime gibi
    /// alanlar olduğu gibi kalır. Sınırı aşan yanıtta üst düzey dizi/nesneler atılır: kayıt her zaman geçerli JSON'dur.
    /// JSON olmayan yanıt kart verisi taşıyabileceği için saklanmaz.</summary>
    public static string? MaskRaw(string? raw)
    {
        JsonObject? json;
        try
        {
            json = string.IsNullOrEmpty(raw) ? null : JsonNode.Parse(raw) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (json is null)
        {
            return null;
        }

        MaskCardFields(json);
        var masked = json.ToJsonString(RawJson);
        if (masked.Length <= RawResponseLimit)
        {
            return masked;
        }

        foreach (var key in json.Where(p => p.Value is JsonObject or JsonArray).Select(p => p.Key).ToList())
        {
            json.Remove(key);
        }

        masked = json.ToJsonString(RawJson);
        return masked.Length <= RawResponseLimit ? masked : null;
    }

    /// <summary>Alan adı → son 4 hane açık kalsın mı.</summary>
    private static readonly Dictionary<string, bool> CardFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cardNumber"] = true,
        ["binNumber"] = true,
        ["cvc"] = false,
        ["cardHolderName"] = false
    };

    private static readonly JsonSerializerOptions RawJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static void MaskCardFields(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                MaskCardFields(item);
            }
        }
        else if (node is JsonObject json)
        {
            foreach (var (key, value) in json.ToList())
            {
                if (value is JsonValue && CardFields.TryGetValue(key, out var keepLastFour))
                {
                    var text = value.ToString();
                    json[key] = keepLastFour && text.Length > 4 ? new string('*', text.Length - 4) + text[^4..] : "***";
                }
                else
                {
                    MaskCardFields(value);
                }
            }
        }
    }

    private sealed class AlreadyClosedException : Exception;

    private sealed class CaptureRejectedException(string message, string? raw) : Exception(message)
    {
        public string? Raw { get; } = raw;
    }
}
