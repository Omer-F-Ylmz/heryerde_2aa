using System.Net;
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

public class OrderManager : IOrderService
{
    private readonly IOrderDal _orderDal;
    private readonly IOrderItemDal _orderItemDal;
    private readonly ICartItemDal _cartItemDal;
    private readonly ICartDal _cartDal;
    private readonly IProductDal _productDal;
    private readonly IProductVariantDal _variantDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;
    private readonly IPaymentDal _paymentDal;
    private readonly IPaymentNoticeDal _noticeDal;
    private readonly IReturnRequestDal _returnDal;
    private readonly IOrderNoteDal _noteDal;
    private readonly ICouponService _coupons;
    private readonly ShopSettings _shop;
    private readonly ShippingSettings _shipping;
    private readonly TimeProvider _clock;

    public OrderManager(
        IOrderDal orderDal,
        IOrderItemDal orderItemDal,
        ICartItemDal cartItemDal,
        ICartDal cartDal,
        IProductDal productDal,
        IProductVariantDal variantDal,
        IUnitOfWork unitOfWork,
        INotificationService notifications,
        IPaymentDal paymentDal,
        IPaymentNoticeDal noticeDal,
        IReturnRequestDal returnDal,
        IOrderNoteDal noteDal,
        ICouponService coupons,
        IOptions<ShopSettings> shop,
        IOptions<ShippingSettings> shipping,
        TimeProvider clock)
    {
        _orderDal = orderDal;
        _orderItemDal = orderItemDal;
        _cartItemDal = cartItemDal;
        _cartDal = cartDal;
        _productDal = productDal;
        _variantDal = variantDal;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _paymentDal = paymentDal;
        _noticeDal = noticeDal;
        _returnDal = returnDal;
        _noteDal = noteDal;
        _coupons = coupons;
        _shop = shop.Value;
        _shipping = shipping.Value;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IDataResult<Order>)> PlaceAsync(Guid cartId, OrderDraft draft, CancellationToken cancellationToken = default)
    {
        if (!PhoneRules.TryNormalize(draft.Phone, out var phone))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>("Geçerli bir cep telefonu yazın (05XX XXX XX XX)."));
        }

        if (string.IsNullOrWhiteSpace(draft.FullName) ||
            string.IsNullOrWhiteSpace(draft.Address) ||
            string.IsNullOrWhiteSpace(draft.City) ||
            string.IsNullOrWhiteSpace(draft.District))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>("Ad soyad, adres, il ve ilçe zorunlu."));
        }

        var items = await _cartItemDal.GetListAsync(i => i.CartId == cartId, cancellationToken);
        if (items.Count == 0)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>("Sepetiniz boş."));
        }

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await _productDal.GetListAsync(p => productIds.Contains(p.Id), cancellationToken)).ToDictionary(p => p.Id);

        var now = _clock.GetUtcNow().UtcDateTime;
        var gifts = GiftRules.Plan(items, await WithGiftProductsAsync(products, now, cancellationToken), now);
        var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
        var shippingFee = ShippingRules.Fee(subtotal, _shop.ShippingFee, _shop.FreeShippingOver);

        // Kupon sipariş anında yeniden doğrulanır: kişi başı limit ancak burada (telefon/e-posta belliyken) bilinir.
        var cart = await _cartDal.GetAsync(c => c.Id == cartId, cancellationToken);
        var coupon = await _coupons.EvaluateAsync(cart?.CouponCode, subtotal, phone, draft.Email?.Trim(), cancellationToken);
        if (cart?.CouponCode is { Length: > 0 } && !coupon.Valid)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>(coupon.Problem ?? "Kupon kodu geçersiz."));
        }

        if (coupon.FreeShipping)
        {
            shippingFee = 0m;
        }

        var order = new Order
        {
            OrderNo = await NextOrderNoAsync(now, cancellationToken),
            AccessToken = Guid.NewGuid(),
            Status = OrderStatus.Beklemede,
            PaymentMethod = draft.PaymentMethod,
            Subtotal = subtotal,
            ShippingFee = shippingFee,
            CouponCode = coupon.Code,
            Discount = coupon.Discount,
            Total = subtotal + shippingFee - coupon.Discount,
            FullName = draft.FullName.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(draft.Email) ? null : draft.Email.Trim(),
            Address = draft.Address.Trim(),
            City = draft.City.Trim(),
            District = draft.District.Trim(),
            Note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim(),
            CreatedAt = now,
            // Ödeme adımı onay kutusunu zorunlu tuttuğu için sipariş anı onay anıdır.
            ConsentAt = now,
            LegalVersion = LegalDocs.Version
        };

        // Kartta stok, sepet ve müşteri postası ödeme onayına kalır; burada yalnız sipariş ve ödeme kaydı açılır.
        var byCard = draft.PaymentMethod == PaymentMethod.KrediKarti;
        var granted = new List<GiftPlan>();
        var placedItems = new List<OrderItem>();
        try
        {
            await _unitOfWork.InTransactionAsync(async () =>
            {
                // Stok kontrolü ve düşümü tek koşullu UPDATE; aynı işlem içinde olduğu için
                // yetersiz kalan satırda önceki düşümler de geri alınır.
                var shortages = new List<string>();
                foreach (var item in byCard ? [] : items)
                {
                    // Giyim'de stok varyantta, Ev'de ürünün kendisinde; stok tutmayan üründe düşüm yok.
                    var decremented = item.VariantId is { } variantId
                        ? await _variantDal.TryDecrementStockAsync(variantId, item.Quantity, cancellationToken)
                        : products.TryGetValue(item.ProductId, out var tracked) && tracked.Stock is not null
                            ? await _productDal.TryDecrementStockAsync(item.ProductId, item.Quantity, cancellationToken)
                            : 1;

                    if (decremented == 0)
                    {
                        shortages.Add(products.TryGetValue(item.ProductId, out var missing) ? missing.Name : "Ürün");
                    }
                }

                if (shortages.Count > 0)
                {
                    throw new StockShortageException(shortages.Distinct().ToList());
                }

                // Hediye stoğu da düşer; bu sırada tükendiyse hediye satırı hiç açılmaz.
                foreach (var gift in gifts.Where(g => g.Available))
                {
                    if (byCard
                        || await _productDal.TryDecrementStockAsync(gift.ProductId, gift.Quantity, cancellationToken) > 0
                        || products.GetValueOrDefault(gift.ProductId)?.Stock is null)
                    {
                        granted.Add(gift);
                    }
                }

                await _orderDal.AddAsync(order, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                foreach (var item in items)
                {
                    var product = products.GetValueOrDefault(item.ProductId);
                    var variant = item.VariantId is { } variantId
                        ? await _variantDal.GetAsync(v => v.Id == variantId, cancellationToken)
                        : null;

                    var line = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductName = product?.Name ?? "Ürün",
                        Sku = variant?.Sku ?? product?.Slug ?? string.Empty,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    };
                    placedItems.Add(line);
                    await _orderItemDal.AddAsync(line, cancellationToken);

                    var cartLine = byCard ? null : await _cartItemDal.GetTrackedAsync(i => i.Id == item.Id, cancellationToken);
                    if (cartLine is not null)
                    {
                        _cartItemDal.Delete(cartLine);
                    }
                }

                foreach (var gift in granted)
                {
                    var giftLine = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductName = gift.ProductName,
                        Sku = gift.Sku,
                        Quantity = gift.Quantity,
                        UnitPrice = 0m,
                        IsGift = true
                    };
                    placedItems.Add(giftLine);
                    await _orderItemDal.AddAsync(giftLine, cancellationToken);
                }

                // Bildirim siparişle aynı işlemde kuyruğa girer: sipariş yazıldıysa postası da kesin kuyruktadır.
                await _notifications.QueueOrderPlacedAsync(order, placedItems, customer: !byCard, cancellationToken: cancellationToken);

                if (byCard)
                {
                    await _paymentDal.AddAsync(new Payment
                    {
                        OrderId = order.Id,
                        CartId = cartId,
                        Provider = PaymentManager.Provider,
                        ConversationId = Guid.NewGuid().ToString("N"),
                        Status = PaymentStatus.Baslatildi,
                        Amount = order.Total,
                        CreatedAt = now,
                        UpdatedAt = now
                    }, cancellationToken);
                }

                return await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (StockShortageException shortage)
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<Order>(
                $"Stok yetersiz: {string.Join(", ", shortage.ProductNames)}. Sepetteki adedi azaltın."));
        }

        // Not, planlanana değil gerçekten yazılan hediyelere bakar: son anda tükenen de mesaja girer.
        var note = GiftRules.Note(gifts.Select(g => g with { Available = granted.Any(x => x.ProductId == g.ProductId) }));
        return (HttpStatusCode.Created, new SuccessDataResult<Order>(
            order,
            note.Length == 0 ? "Siparişiniz alındı." : "Siparişiniz alındı. " + note));
    }

    public async Task<(HttpStatusCode, IDataResult<Order>)> PlaceManualAsync(ManualOrderDraft draft, CancellationToken cancellationToken = default)
    {
        if (!PhoneRules.TryNormalize(draft.Phone, out var phone))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>("Geçerli bir cep telefonu yazın (05XX XXX XX XX)."));
        }

        if (string.IsNullOrWhiteSpace(draft.FullName) ||
            string.IsNullOrWhiteSpace(draft.Address) ||
            string.IsNullOrWhiteSpace(draft.City) ||
            string.IsNullOrWhiteSpace(draft.District))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>("Ad soyad, adres, il ve ilçe zorunlu."));
        }

        // Kartlı sipariş yönetimden açılmaz: çekim yalnız müşterinin 3D doğrulamasıyla olur.
        if (draft.PaymentMethod is not (PaymentMethod.KapidaOdeme or PaymentMethod.HavaleEft or PaymentMethod.NakitElden)
            || draft.Source == OrderSource.Site)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>("Kaynak ve ödeme yöntemi seçin (kart yönetimden alınmaz)."));
        }

        var lines = draft.Lines.Where(l => !string.IsNullOrWhiteSpace(l.Code)).ToList();
        if (lines.Count == 0 || lines.Any(l => l.Quantity < 1))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>("En az bir kalem yazın; adet 1 ya da daha fazla olmalı."));
        }

        if (draft.ShippingFeeOverride is < 0m)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>("Kargo ücreti eksi olamaz."));
        }

        // Mesafeli satış (WhatsApp, Instagram, telefon): ön bilgilendirme ve sözleşme müşteriye iletilip teyit alınmış olmalı.
        var remote = draft.Source != OrderSource.Magaza;
        if (remote && !draft.ConsentConfirmed)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>(
                "Uzaktan siparişte ön bilgilendirme formu ve mesafeli satış sözleşmesi müşteriye iletilip teyidi alınmalı."));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var resolved = new List<(ManualOrderLine Line, Product Product, ProductVariant? Variant)>();
        foreach (var line in lines)
        {
            var code = line.Code.Trim();
            var variant = await _variantDal.GetAsync(v => v.Sku == code, cancellationToken);
            var product = variant is not null
                ? await _productDal.GetAsync(p => p.Id == variant.ProductId, cancellationToken)
                : await _productDal.GetAsync(p => p.Slug == code && p.DeletedAt == null, cancellationToken);

            // Varyantlı ürün slug'la seçilemez: stok varyantta, hangi beden/renk olduğu bilinmeli.
            if (product is null || product.DeletedAt is not null
                || variant is null && await _variantDal.GetAsync(v => v.ProductId == product.Id, cancellationToken) is not null)
            {
                return (HttpStatusCode.BadRequest, new ErrorDataResult<Order>(
                    $"'{code}' bulunamadı; varyantlı üründe stok kodunu (ör. SALVAR-M), varyantsız üründe slug'ı yazın."));
            }

            resolved.Add((line with { Code = code }, product, variant));
        }

        // Hediye kampanyası vitrindekiyle aynı kuraldan; yönetici kutuyu kapatırsa hiç uygulanmaz.
        var products = resolved.ToDictionary(r => r.Product.Id, r => r.Product);
        var giftLines = draft.ApplyGifts
            ? resolved.Select(r => new CartItem { ProductId = r.Product.Id, Quantity = r.Line.Quantity }).ToList()
            : [];
        var gifts = giftLines.Count == 0
            ? []
            : GiftRules.Plan(giftLines, await WithGiftProductsAsync(products, now, cancellationToken), now);

        var subtotal = resolved.Sum(r => CurrentPrice(r.Product, now) * r.Line.Quantity);
        var shippingFee = draft.ShippingFeeOverride ?? ShippingRules.Fee(subtotal, _shop.ShippingFee, _shop.FreeShippingOver);
        var order = new Order
        {
            OrderNo = await NextOrderNoAsync(now, cancellationToken),
            AccessToken = Guid.NewGuid(),
            Status = OrderStatus.Beklemede,
            PaymentMethod = draft.PaymentMethod,
            Source = draft.Source,
            Subtotal = subtotal,
            ShippingFee = shippingFee,
            ShippingOverridden = draft.ShippingFeeOverride is not null,
            Total = subtotal + shippingFee,
            FullName = draft.FullName.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(draft.Email) ? null : draft.Email.Trim(),
            Address = draft.Address.Trim(),
            City = draft.City.Trim(),
            District = draft.District.Trim(),
            Note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim(),
            // Uzaktan siparişte teyit anı ve metin sürümü yöneticinin işaretlediği teyitten; mağazada boş kalır.
            ConsentAt = remote ? now : null,
            LegalVersion = remote ? LegalDocs.Version : null,
            CreatedAt = now
        };

        var granted = new List<GiftPlan>();
        try
        {
            await _unitOfWork.InTransactionAsync(async () =>
            {
                var shortages = new List<string>();
                foreach (var (line, product, variant) in resolved)
                {
                    var decremented = variant is not null
                        ? await _variantDal.TryDecrementStockAsync(variant.Id, line.Quantity, cancellationToken)
                        : product.Stock is not null
                            ? await _productDal.TryDecrementStockAsync(product.Id, line.Quantity, cancellationToken)
                            : 1;
                    if (decremented == 0)
                    {
                        shortages.Add(product.Name);
                    }
                }

                if (shortages.Count > 0)
                {
                    throw new StockShortageException(shortages.Distinct().ToList());
                }

                // Hediye stoğu da düşer; bu sırada tükendiyse hediye satırı hiç açılmaz.
                foreach (var gift in gifts.Where(g => g.Available))
                {
                    if (await _productDal.TryDecrementStockAsync(gift.ProductId, gift.Quantity, cancellationToken) > 0
                        || products.GetValueOrDefault(gift.ProductId)?.Stock is null)
                    {
                        granted.Add(gift);
                    }
                }

                await _orderDal.AddAsync(order, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var items = new List<OrderItem>();
                foreach (var (line, product, variant) in resolved)
                {
                    var item = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductName = product.Name,
                        Sku = variant?.Sku ?? product.Slug,
                        Quantity = line.Quantity,
                        UnitPrice = CurrentPrice(product, now)
                    };
                    items.Add(item);
                    await _orderItemDal.AddAsync(item, cancellationToken);
                }

                foreach (var gift in granted)
                {
                    var giftItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductName = gift.ProductName,
                        Sku = gift.Sku,
                        Quantity = gift.Quantity,
                        UnitPrice = 0m,
                        IsGift = true
                    };
                    items.Add(giftItem);
                    await _orderItemDal.AddAsync(giftItem, cancellationToken);
                }

                // Mağaza postası yok: siparişi yönetici kendisi girdi.
                await _notifications.QueueOrderPlacedAsync(order, items, customer: draft.NotifyCustomer, store: false, cancellationToken: cancellationToken);
                return await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (StockShortageException shortage)
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<Order>(
                $"Stok yetersiz: {string.Join(", ", shortage.ProductNames)}."));
        }

        // Not, planlanana değil gerçekten yazılan hediyelere bakar: son anda tükenen de mesaja girer.
        var giftNote = GiftRules.Note(gifts.Select(g => g with { Available = granted.Any(x => x.ProductId == g.ProductId) }));
        return (HttpStatusCode.Created, new SuccessDataResult<Order>(
            order,
            giftNote.Length == 0 ? "Sipariş açıldı." : "Sipariş açıldı. " + giftNote));
    }

    public async Task<(HttpStatusCode, IDataResult<string>)> EditAsync(int orderId, OrderEdit edit, CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<string>("Sipariş bulunamadı."));
        }

        if (!OrderRules.CanEdit(order.Status))
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<string>("Hazırlanmaya ya da kargoya geçmiş sipariş düzenlenemez."));
        }

        if (!PhoneRules.TryNormalize(edit.Phone, out var phone))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<string>("Geçerli bir cep telefonu yazın (05XX XXX XX XX)."));
        }

        if (string.IsNullOrWhiteSpace(edit.Address) || string.IsNullOrWhiteSpace(edit.City) || string.IsNullOrWhiteSpace(edit.District))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<string>("Adres, il ve ilçe zorunlu."));
        }

        var items = await _orderItemDal.GetListAsync(i => i.OrderId == order.Id && !i.IsGift, cancellationToken);
        var changes = new List<(OrderItem Item, int Quantity)>();
        foreach (var (itemId, quantity) in edit.Quantities)
        {
            var item = items.FirstOrDefault(i => i.Id == itemId);
            if (item is null || quantity < 1)
            {
                return (HttpStatusCode.BadRequest, new ErrorDataResult<string>("Kalem adedi 1 ya da daha fazla olmalı."));
            }

            if (item.Quantity != quantity)
            {
                changes.Add((item, quantity));
            }
        }

        // Kartta çekilen tutar sabittir; adet değişirse tahsilatla sipariş ayrışırdı.
        if (changes.Count > 0 && order.PaymentMethod == PaymentMethod.KrediKarti)
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<string>("Kartla ödenmiş siparişte adet değişmez; gerekirse iade edin."));
        }

        // Onaylı havalede de para alınmıştır: toplam değişirse onaylanan havale tutarıyla ayrışırdı.
        if (changes.Count > 0 && order.PaymentMethod == PaymentMethod.HavaleEft && order.Status != OrderStatus.Beklemede)
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<string>("Havalesi onaylanmış siparişte adet değişmez; yeni sipariş açın ya da iptal edin."));
        }

        var log = new List<string>();
        void Track(string label, string? before, string? after)
        {
            if ((before ?? string.Empty) != (after ?? string.Empty))
            {
                log.Add($"{label}: {before} → {after}");
            }
        }

        var note = string.IsNullOrWhiteSpace(edit.Note) ? null : edit.Note.Trim();
        Track("adres", order.Address, edit.Address.Trim());
        Track("il", order.City, edit.City.Trim());
        Track("ilçe", order.District, edit.District.Trim());
        Track("telefon", order.Phone, phone);
        Track("not", order.Note, note);

        try
        {
            await _unitOfWork.InTransactionAsync(async () =>
            {
                foreach (var (item, quantity) in changes)
                {
                    var diff = quantity - item.Quantity;
                    if (diff > 0 && !await TryTakeStockAsync(item.Sku, diff, cancellationToken))
                    {
                        throw new StockShortageException([item.ProductName]);
                    }

                    if (diff < 0)
                    {
                        await ReturnStockAsync(item.Sku, -diff, cancellationToken);
                    }

                    var tracked = (await _orderItemDal.GetTrackedAsync(i => i.Id == item.Id, cancellationToken))!;
                    log.Add($"{item.ProductName} adet: {item.Quantity} → {quantity}");
                    tracked.Quantity = quantity;
                    item.Quantity = quantity;
                }

                order.Address = edit.Address.Trim();
                order.City = edit.City.Trim();
                order.District = edit.District.Trim();
                order.Phone = phone;
                order.Note = note;
                order.Subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
                // Kargo ücreti yeni ara toplama göre yeniden hesaplanır; yönetici elle yazdıysa ona dokunulmaz.
                if (!order.ShippingOverridden)
                {
                    var recalculated = ShippingRules.Fee(order.Subtotal, _shop.ShippingFee, _shop.FreeShippingOver);
                    Track("kargo", order.ShippingFee.ToString("0.00"), recalculated.ToString("0.00"));
                    order.ShippingFee = recalculated;
                }

                order.Total = order.Subtotal + order.ShippingFee;
                return await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (StockShortageException shortage)
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<string>($"Stok yetersiz: {string.Join(", ", shortage.ProductNames)}."));
        }

        return (HttpStatusCode.OK, new SuccessDataResult<string>(string.Join("; ", log), "Sipariş güncellendi."));
    }

    public async Task<(HttpStatusCode, IDataResult<string?>)> SetInvoiceAsync(
        int orderId,
        string invoiceNo,
        DateTime invoiceDate,
        string file,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<string?>("Sipariş bulunamadı."));
        }

        if (string.IsNullOrWhiteSpace(invoiceNo) || invoiceNo.Trim().Length > 40)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<string?>("Fatura numarası zorunlu (en çok 40 karakter)."));
        }

        var previous = order.InvoiceFile;
        order.InvoiceNo = invoiceNo.Trim();
        order.InvoiceDate = invoiceDate.Date;
        order.InvoiceFile = file;
        // Posta faturayla aynı kaydetmede kuyruğa girer: fatura yazıldıysa haberi de kesin kuyruktadır.
        await _notifications.QueueInvoiceReadyAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<string?>(previous, "Fatura kaydedildi."));
    }

    public async Task<(HttpStatusCode, IResult)> SubmitPaymentNoticeAsync(
        string orderNo,
        Guid accessToken,
        PaymentNoticeDraft draft,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetAsync(o => o.OrderNo == orderNo && o.AccessToken == accessToken, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sipariş bulunamadı."));
        }

        if (order.PaymentMethod != PaymentMethod.HavaleEft || order.Status != OrderStatus.Beklemede)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Bu sipariş için havale bildirimi beklenmiyor."));
        }

        if (string.IsNullOrWhiteSpace(draft.SenderName) || draft.SenderName.Trim().Length > 120
            || draft.Amount <= 0m || draft.PaidOn == DateTime.MinValue)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Gönderen adı, havale tarihi ve tutar zorunlu."));
        }

        await _noticeDal.AddAsync(new PaymentNotice
        {
            OrderId = order.Id,
            SenderName = draft.SenderName.Trim(),
            PaidOn = draft.PaidOn.Date,
            Amount = draft.Amount,
            ReceiptFile = draft.ReceiptFile,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Bildiriminiz alındı; hesabımıza geçince siparişiniz onaylanır."));
    }

    public async Task<(HttpStatusCode, IResult)> ApprovePaymentAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sipariş bulunamadı."));
        }

        if (order.PaymentMethod != PaymentMethod.HavaleEft || !OrderRules.CanTransition(order.Status, OrderStatus.Onaylandi))
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Yalnız bekleyen havale siparişi onaylanır."));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var notice in await _noticeDal.GetListAsync(n => n.OrderId == order.Id && n.ApprovedAt == null, cancellationToken))
        {
            (await _noticeDal.GetTrackedAsync(n => n.Id == notice.Id, cancellationToken))!.ApprovedAt = now;
        }

        order.Status = OrderStatus.Onaylandi;
        await _notifications.QueuePaymentApprovedAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Havale onaylandı; müşteriye posta gidecek."));
    }

    public async Task<(HttpStatusCode, IDataResult<OrderDetail>)> GetByOrderNoAsync(string orderNo, CancellationToken cancellationToken = default)
        => await DetailAsync(await _orderDal.GetAsync(o => o.OrderNo == orderNo, cancellationToken), cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<OrderDetail>)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await DetailAsync(await _orderDal.GetAsync(o => o.Id == id, cancellationToken), cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<Order>)> LookupAsync(string orderNo, string phone, CancellationToken cancellationToken = default)
    {
        var number = orderNo.Trim().ToUpperInvariant();
        var order = PhoneRules.TryNormalize(phone, out var normalized)
            ? await _orderDal.GetAsync(o => o.OrderNo == number && o.Phone == normalized, cancellationToken)
            : null;

        return order is null
            ? (HttpStatusCode.NotFound, new ErrorDataResult<Order>("Bu sipariş numarası ve telefonla eşleşen sipariş bulunamadı."))
            : (HttpStatusCode.OK, new SuccessDataResult<Order>(order));
    }

    public async Task<(HttpStatusCode, IDataResult<List<Order>>)> SearchAsync(
        OrderStatus? status,
        string? query,
        bool uninvoicedDelivered = false,
        CancellationToken cancellationToken = default)
    {
        var orders = await _orderDal.GetListAsync(
            uninvoicedDelivered
                ? o => o.Status == OrderStatus.TeslimEdildi && o.InvoiceNo == null
                : status is { } wanted ? o => o.Status == wanted : null,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            var phone = PhoneRules.TryNormalize(term, out var normalized) ? normalized : term;
            orders = orders
                .Where(o => o.OrderNo.Contains(term, StringComparison.OrdinalIgnoreCase) || o.Phone.Contains(phone, StringComparison.Ordinal))
                .ToList();
        }

        return (HttpStatusCode.OK, new SuccessDataResult<List<Order>>(orders.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id).ToList()));
    }

    public async Task<(HttpStatusCode, IResult)> ChangeStatusAsync(
        int orderId,
        OrderStatus next,
        string? carrier = null,
        string? trackingNo = null,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sipariş bulunamadı."));
        }

        if (!OrderRules.CanTransition(order.Status, next))
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(
                $"'{order.Status}' durumundan '{next}' durumuna geçilemez; durum geri alınamaz."));
        }

        // Kartlı siparişte stok ve para ödeme onayında gelir; onaylanmamış ödemede ikisi de yoktur.
        var payment = order.PaymentMethod == PaymentMethod.KrediKarti
            ? (await _paymentDal.GetListAsync(p => p.OrderId == order.Id, cancellationToken)).MaxBy(p => p.Id)
            : null;
        var unpaidCard = order.PaymentMethod == PaymentMethod.KrediKarti && payment?.Status != PaymentStatus.Basarili;

        if (unpaidCard && next == OrderStatus.Onaylandi)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Kart ödemesi alınmamış sipariş onaylanamaz."));
        }

        if (next == OrderStatus.Kargoda)
        {
            var firm = carrier?.Trim();
            var code = trackingNo?.Trim();
            if (string.IsNullOrEmpty(firm) || string.IsNullOrEmpty(code))
            {
                return (HttpStatusCode.BadRequest, new ErrorResult(
                    "Kargoya verirken kargo firması ve takip numarası zorunlu."));
            }

            if (!_shipping.Knows(firm))
            {
                return (HttpStatusCode.BadRequest, new ErrorResult($"'{firm}' tanımlı bir kargo firması değil."));
            }

            order.Carrier = firm;
            order.TrackingNo = code;
            order.Status = next;
            await _notifications.QueueOrderShippedAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return (HttpStatusCode.OK, new SuccessResult("Sipariş kargoya verildi; takip bilgisi müşteriye gidecek."));
        }

        if (next == OrderStatus.IptalEdildi)
        {
            // İade ve durum aynı işlemde: durum yazılamazsa stok da geri eklenmiş sayılmaz.
            await _unitOfWork.InTransactionAsync(async () =>
            {
                // Açık ödeme kaydı kapatılır: sonradan gelen 3D dönüşü iptal edilmiş siparişten çekim yapamaz.
                // Kapatılamadıysa bir dönüş kaydı az önce kapatmıştır; çekim geçtiyse stok düşmüştür ve iade edilir.
                var stockTaken = !unpaidCard;
                if (unpaidCard && payment is not null
                    && await _paymentDal.TryCloseAsync(payment.Id, PaymentStatus.Basarisiz, _clock.GetUtcNow().UtcDateTime, cancellationToken) == 0)
                {
                    stockTaken = (await _paymentDal.GetAsync(p => p.Id == payment.Id, cancellationToken))!.Status == PaymentStatus.Basarili;
                }

                foreach (var item in stockTaken ? await _orderItemDal.GetListAsync(i => i.OrderId == order.Id, cancellationToken) : [])
                {
                    await ReturnStockAsync(item.Sku, item.Quantity, cancellationToken);
                }

                order.Status = next;
                return await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, cancellationToken);

            return (HttpStatusCode.OK, new SuccessResult("Sipariş iptal edildi, stok geri alındı."));
        }

        order.Status = next;
        if (next == OrderStatus.TeslimEdildi)
        {
            // 14 günlük cayma/iade talebi süresi teslim anından sayılır.
            order.DeliveredAt = _clock.GetUtcNow().UtcDateTime;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Sipariş durumu güncellendi."));
    }

    public async Task<List<OrderDetail>> ExportAsync(OrderStatus? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var orders = await _orderDal.GetListAsync(
            o => (status == null || o.Status == status)
                 && (fromUtc == null || o.CreatedAt >= fromUtc)
                 && (toUtc == null || o.CreatedAt < toUtc),
            cancellationToken);
        var ids = orders.Select(o => o.Id).ToList();
        var items = (ids.Count == 0 ? new List<OrderItem>() : await _orderItemDal.GetListAsync(i => ids.Contains(i.OrderId), cancellationToken))
            .ToLookup(i => i.OrderId);

        return orders
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Select(o => new OrderDetail(o, items[o.Id].OrderBy(i => i.Id).ToList()))
            .ToList();
    }

    public async Task<(HttpStatusCode, IDataResult<int>)> UnseenCountAsync(CancellationToken cancellationToken = default)
        => (HttpStatusCode.OK, new SuccessDataResult<int>(await _orderDal.UnseenCountAsync(cancellationToken)));

    public async Task<(HttpStatusCode, IResult)> MarkSeenAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sipariş bulunamadı."));
        }

        // İlk açılış rozeti düşürür; sonraki açılışlar ilk görülme anını korur.
        if (order.SeenAt is null)
        {
            order.SeenAt = _clock.GetUtcNow().UtcDateTime;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (HttpStatusCode.OK, new SuccessResult("Sipariş görüldü olarak işaretlendi."));
    }

    public async Task<(HttpStatusCode, IDataResult<IReadOnlyList<string>>)> AnonymizeAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderDal.GetTrackedAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<IReadOnlyList<string>>("Sipariş bulunamadı."));
        }

        if (!OrderRules.CanAnonymize(order.Status))
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<IReadOnlyList<string>>(
                "Kişisel veri yalnız teslim edilmiş ya da iptal edilmiş siparişte anonimleştirilir."));
        }

        order.FullName = PersonalDataMask.Name(order.FullName);
        order.Phone = PersonalDataMask.Phone(order.Phone);
        order.Email = order.Email is null ? null : PersonalDataMask.Email(order.Email);
        order.Address = PersonalDataMask.Hidden;
        order.Note = null;
        // Postadaki ya da tarayıcı geçmişindeki eski bağlantıyla sipariş sayfası ve fatura artık açılmaz.
        order.AccessToken = Guid.NewGuid();
        // Havaleyi gönderen başka biri olabilir; adı da siparişle birlikte maskelenir, tutar ve tarih kalır.
        // Dekont (ad, IBAN) saklanmaz: kayıttan düşer, dosyası çağıran tarafından silinir. Fatura yasal belge olarak kalır.
        var receipts = new List<string>();
        foreach (var notice in await _noticeDal.GetListAsync(n => n.OrderId == order.Id, cancellationToken))
        {
            var tracked = (await _noticeDal.GetTrackedAsync(n => n.Id == notice.Id, cancellationToken))!;
            tracked.SenderName = PersonalDataMask.Name(tracked.SenderName);
            if (tracked.ReceiptFile is { } receipt)
            {
                receipts.Add(receipt);
                tracked.ReceiptFile = null;
            }
        }

        // İade talebinde neden serbest metin, IBAN ve fotoğraf kişiye ait: silinir; tür, durum, kalem ve tutar ispat için kalır.
        order.RefundIban = null;
        foreach (var request in await _returnDal.GetListAsync(r => r.OrderId == order.Id, cancellationToken))
        {
            var tracked = (await _returnDal.GetTrackedAsync(r => r.Id == request.Id, cancellationToken))!;
            tracked.Reason = PersonalDataMask.Hidden;
            tracked.RejectReason = tracked.RejectReason is null ? null : PersonalDataMask.Hidden;
            tracked.RefundIban = null;
            if (tracked.PhotoFile is { } photo)
            {
                receipts.Add(photo);
                tracked.PhotoFile = null;
            }
        }

        // İç not serbest metin (ad, telefon geçebilir): metin gizlenir, notun kim tarafından ne zaman düşüldüğü kalır.
        foreach (var note in await _noteDal.GetListAsync(n => n.OrderId == order.Id, cancellationToken))
        {
            (await _noteDal.GetTrackedAsync(n => n.Id == note.Id, cancellationToken))!.Text = PersonalDataMask.Hidden;
        }

        await _notifications.ForgetOrderAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<IReadOnlyList<string>>(receipts, "Kişisel veri anonimleştirildi."));
    }

    private async Task<(HttpStatusCode, IDataResult<OrderDetail>)> DetailAsync(Order? order, CancellationToken cancellationToken)
    {
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<OrderDetail>("Sipariş bulunamadı."));
        }

        var items = await _orderItemDal.GetListAsync(i => i.OrderId == order.Id, cancellationToken);
        var payment = order.PaymentMethod == PaymentMethod.KrediKarti
            ? (await _paymentDal.GetListAsync(p => p.OrderId == order.Id, cancellationToken)).MaxBy(p => p.Id)
            : null;
        var notices = order.PaymentMethod == PaymentMethod.HavaleEft
            ? (await _noticeDal.GetListAsync(n => n.OrderId == order.Id, cancellationToken)).OrderBy(n => n.Id).ToList()
            : null;
        return (HttpStatusCode.OK, new SuccessDataResult<OrderDetail>(new OrderDetail(order, items.OrderBy(i => i.Id).ToList(), payment, notices)));
    }

    /// <summary>Stok kodu varyanta uymuyorsa satır varyantsız (Ev) üründür; iade ürünün stoğuna yazılır.</summary>
    private async Task ReturnStockAsync(string sku, int quantity, CancellationToken cancellationToken)
    {
        if (await _variantDal.IncrementStockBySkuAsync(sku, quantity, cancellationToken) == 0)
        {
            await _productDal.IncrementStockBySlugAsync(sku, quantity, cancellationToken);
        }
    }

    /// <summary>Satırın stok kodu varyantsa varyanttan, değilse ürünün kendisinden düşer; stok tutmayan üründe düşüm yok.</summary>
    private async Task<bool> TryTakeStockAsync(string sku, int quantity, CancellationToken cancellationToken)
    {
        if (await _variantDal.GetAsync(v => v.Sku == sku, cancellationToken) is { } variant)
        {
            return await _variantDal.TryDecrementStockAsync(variant.Id, quantity, cancellationToken) > 0;
        }

        var product = await _productDal.GetAsync(p => p.Slug == sku, cancellationToken);
        return product?.Stock is null || await _productDal.TryDecrementStockAsync(product.Id, quantity, cancellationToken) > 0;
    }

    private static decimal CurrentPrice(Product product, DateTime now)
        => ProductRules.CampaignIsActive(product, now) ? product.CampaignPrice!.Value : product.Price;

    /// <summary>Hediye edilen başka ürünler de sözlüğe girsin; adı ve stoğu oradan okunur.</summary>
    private async Task<Dictionary<int, Product>> WithGiftProductsAsync(
        Dictionary<int, Product> products,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var missing = products.Values
            .Where(p => GiftRules.IsActive(p, now))
            .Select(p => p.GiftProductId)
            .OfType<int>()
            .Where(id => !products.ContainsKey(id))
            .Distinct()
            .ToList();

        foreach (var gift in missing.Count == 0
                     ? []
                     : await _productDal.GetListAsync(p => missing.Contains(p.Id), cancellationToken))
        {
            products[gift.Id] = gift;
        }

        return products;
    }

    private async Task<string> NextOrderNoAsync(DateTime moment, CancellationToken cancellationToken)
        => OrderNo.Build(moment, await _orderDal.NextOrderSequenceAsync(cancellationToken));

    /// <summary>Yetersiz stokta işlemi geri almak için; dışarı sızmaz, 409'a çevrilir.</summary>
    private sealed class StockShortageException(IReadOnlyList<string> productNames) : Exception
    {
        public IReadOnlyList<string> ProductNames { get; } = productNames;
    }
}
