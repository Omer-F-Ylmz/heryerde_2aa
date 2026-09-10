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
    private readonly IProductDal _productDal;
    private readonly IProductVariantDal _variantDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ShopSettings _shop;
    private readonly TimeProvider _clock;

    public OrderManager(
        IOrderDal orderDal,
        IOrderItemDal orderItemDal,
        ICartItemDal cartItemDal,
        IProductDal productDal,
        IProductVariantDal variantDal,
        IUnitOfWork unitOfWork,
        IOptions<ShopSettings> shop,
        TimeProvider clock)
    {
        _orderDal = orderDal;
        _orderItemDal = orderItemDal;
        _cartItemDal = cartItemDal;
        _productDal = productDal;
        _variantDal = variantDal;
        _unitOfWork = unitOfWork;
        _shop = shop.Value;
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
        var order = new Order
        {
            OrderNo = await NextOrderNoAsync(now, cancellationToken),
            AccessToken = Guid.NewGuid(),
            Status = OrderStatus.Beklemede,
            PaymentMethod = draft.PaymentMethod,
            Subtotal = subtotal,
            ShippingFee = _shop.ShippingFee,
            Total = subtotal + _shop.ShippingFee,
            FullName = draft.FullName.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(draft.Email) ? null : draft.Email.Trim(),
            Address = draft.Address.Trim(),
            City = draft.City.Trim(),
            District = draft.District.Trim(),
            Note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim(),
            CreatedAt = now
        };

        var granted = new List<GiftPlan>();
        try
        {
            await _unitOfWork.InTransactionAsync(async () =>
            {
                // Stok kontrolü ve düşümü tek koşullu UPDATE; aynı işlem içinde olduğu için
                // yetersiz kalan satırda önceki düşümler de geri alınır.
                var shortages = new List<string>();
                foreach (var item in items)
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
                    if (await _productDal.TryDecrementStockAsync(gift.ProductId, gift.Quantity, cancellationToken) > 0
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

                    await _orderItemDal.AddAsync(new OrderItem
                    {
                        OrderId = order.Id,
                        ProductName = product?.Name ?? "Ürün",
                        Sku = variant?.Sku ?? product?.Slug ?? string.Empty,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    }, cancellationToken);

                    var line = await _cartItemDal.GetTrackedAsync(i => i.Id == item.Id, cancellationToken);
                    if (line is not null)
                    {
                        _cartItemDal.Delete(line);
                    }
                }

                foreach (var gift in granted)
                {
                    await _orderItemDal.AddAsync(new OrderItem
                    {
                        OrderId = order.Id,
                        ProductName = gift.ProductName,
                        Sku = gift.Sku,
                        Quantity = gift.Quantity,
                        UnitPrice = 0m,
                        IsGift = true
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

    public async Task<(HttpStatusCode, IDataResult<OrderDetail>)> GetByOrderNoAsync(string orderNo, CancellationToken cancellationToken = default)
        => await DetailAsync(await _orderDal.GetAsync(o => o.OrderNo == orderNo, cancellationToken), cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<OrderDetail>)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await DetailAsync(await _orderDal.GetAsync(o => o.Id == id, cancellationToken), cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<List<Order>>)> SearchAsync(OrderStatus? status, string? query, CancellationToken cancellationToken = default)
    {
        var orders = await _orderDal.GetListAsync(
            status is { } wanted ? o => o.Status == wanted : null,
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

    public async Task<(HttpStatusCode, IResult)> ChangeStatusAsync(int orderId, OrderStatus next, CancellationToken cancellationToken = default)
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

        if (next == OrderStatus.IptalEdildi)
        {
            // İade ve durum aynı işlemde: durum yazılamazsa stok da geri eklenmiş sayılmaz.
            await _unitOfWork.InTransactionAsync(async () =>
            {
                foreach (var item in await _orderItemDal.GetListAsync(i => i.OrderId == order.Id, cancellationToken))
                {
                    // Stok kodu varyanta uymuyorsa satır varyantsız (Ev) üründür; iade ürünün stoğuna yazılır.
                    if (await _variantDal.IncrementStockBySkuAsync(item.Sku, item.Quantity, cancellationToken) == 0)
                    {
                        await _productDal.IncrementStockBySlugAsync(item.Sku, item.Quantity, cancellationToken);
                    }
                }

                order.Status = next;
                return await _unitOfWork.SaveChangesAsync(cancellationToken);
            }, cancellationToken);

            return (HttpStatusCode.OK, new SuccessResult("Sipariş iptal edildi, stok geri alındı."));
        }

        order.Status = next;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Sipariş durumu güncellendi."));
    }

    private async Task<(HttpStatusCode, IDataResult<OrderDetail>)> DetailAsync(Order? order, CancellationToken cancellationToken)
    {
        if (order is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<OrderDetail>("Sipariş bulunamadı."));
        }

        var items = await _orderItemDal.GetListAsync(i => i.OrderId == order.Id, cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<OrderDetail>(new OrderDetail(order, items.OrderBy(i => i.Id).ToList())));
    }

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
