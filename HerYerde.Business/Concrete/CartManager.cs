using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using Microsoft.Extensions.Options;

namespace HerYerde.Business.Concrete;

public class CartManager : ICartService
{
    private readonly ICartDal _cartDal;
    private readonly ICartItemDal _itemDal;
    private readonly IProductDal _productDal;
    private readonly IProductVariantDal _variantDal;
    private readonly IProductImageDal _imageDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ShopSettings _shop;
    private readonly TimeProvider _clock;

    public CartManager(
        ICartDal cartDal,
        ICartItemDal itemDal,
        IProductDal productDal,
        IProductVariantDal variantDal,
        IProductImageDal imageDal,
        IUnitOfWork unitOfWork,
        IOptions<ShopSettings> shop,
        TimeProvider clock)
    {
        _cartDal = cartDal;
        _itemDal = itemDal;
        _productDal = productDal;
        _variantDal = variantDal;
        _imageDal = imageDal;
        _unitOfWork = unitOfWork;
        _shop = shop.Value;
        _clock = clock;
    }

    public async Task<int> PurgeStaleAsync(TimeSpan age, CancellationToken cancellationToken = default)
    {
        var limit = _clock.GetUtcNow().UtcDateTime - age;
        var stale = await _cartDal.GetListAsync(c => c.CreatedAt < limit, cancellationToken);
        if (stale.Count == 0)
        {
            return 0;
        }

        var ids = stale.Select(c => c.Id).ToList();
        foreach (var item in await _itemDal.GetListAsync(i => ids.Contains(i.CartId), cancellationToken))
        {
            _itemDal.Delete(item);
        }

        foreach (var cart in stale)
        {
            _cartDal.Delete(cart);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }

    public async Task<(HttpStatusCode, IDataResult<Cart>)> GetOrCreateAsync(Guid? cartId, CancellationToken cancellationToken = default)
    {
        if (cartId is { } id && await _cartDal.GetAsync(c => c.Id == id, cancellationToken) is { } existing)
        {
            return (HttpStatusCode.OK, new SuccessDataResult<Cart>(existing));
        }

        var cart = new Cart { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        await _cartDal.AddAsync(cart, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<Cart>(cart));
    }

    public async Task<(HttpStatusCode, IDataResult<CartView>)> GetAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var items = await _itemDal.GetListAsync(i => i.CartId == cartId, cancellationToken);
        if (items.Count == 0)
        {
            return (HttpStatusCode.OK, new SuccessDataResult<CartView>(new CartView(cartId, [], 0m, 0m)));
        }

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await _productDal.GetListAsync(p => productIds.Contains(p.Id), cancellationToken)).ToDictionary(p => p.Id);
        // Varyantsız (Ev) sepette varyant tablosuna hiç gidilmez.
        var variants = items.Any(i => i.VariantId is not null)
            ? (await _variantDal.GetListAsync(v => productIds.Contains(v.ProductId), cancellationToken)).ToDictionary(v => v.Id)
            : [];
        var images = await _imageDal.GetListAsync(i => productIds.Contains(i.ProductId), cancellationToken);

        var lines = new List<CartLine>();
        foreach (var item in items.OrderBy(i => i.Id))
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                continue;
            }

            var variant = item.VariantId is { } variantId ? variants.GetValueOrDefault(variantId) : null;
            var image = images
                .Where(i => i.ProductId == item.ProductId)
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .FirstOrDefault();

            lines.Add(new CartLine(
                item.Id,
                product.Id,
                product.Name,
                product.Slug,
                image?.Url,
                variant?.Size,
                variant?.Color,
                item.Quantity,
                item.UnitPrice,
                variant?.Stock ?? 0,
                variant is not null));
        }

        var subtotal = lines.Sum(l => l.LineTotal);
        var shipping = lines.Count == 0 ? 0m : _shop.ShippingFee;
        return (HttpStatusCode.OK, new SuccessDataResult<CartView>(new CartView(cartId, lines, subtotal, shipping)));
    }

    public async Task<(HttpStatusCode, IResult)> AddAsync(Guid cartId, int productId, int? variantId, int quantity, CancellationToken cancellationToken = default)
    {
        if (await _cartDal.GetAsync(c => c.Id == cartId, cancellationToken) is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sepet bulunamadı."));
        }

        var product = await _productDal.GetAsync(p => p.Id == productId && p.IsActive, cancellationToken);
        if (product is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Ürün bulunamadı."));
        }

        ProductVariant? variant = null;
        if (variantId is { } id)
        {
            variant = await _variantDal.GetAsync(v => v.Id == id && v.ProductId == productId, cancellationToken);
            if (variant is null)
            {
                return (HttpStatusCode.NotFound, new ErrorResult("Seçilen varyant bulunamadı."));
            }
        }

        var existing = await _itemDal.GetTrackedAsync(
            i => i.CartId == cartId && i.ProductId == productId && i.VariantId == variantId,
            cancellationToken);

        // Satır birleştiğinde tavan toplam adet üzerinden bakılır.
        if (QuantityProblem(quantity + (existing?.Quantity ?? 0), variant) is { } problem)
        {
            return problem;
        }

        if (existing is null)
        {
            await _itemDal.AddAsync(new CartItem
            {
                CartId = cartId,
                ProductId = productId,
                VariantId = variantId,
                Quantity = quantity,
                UnitPrice = CurrentPrice(product)
            }, cancellationToken);
        }
        else
        {
            existing.Quantity += quantity;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Ürün sepete eklendi."));
    }

    public async Task<(HttpStatusCode, IResult)> SetQuantityAsync(Guid cartId, int itemId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity < 0)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Adet negatif olamaz."));
        }

        var item = await _itemDal.GetTrackedAsync(i => i.Id == itemId && i.CartId == cartId, cancellationToken);
        if (item is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sepet satırı bulunamadı."));
        }

        if (quantity == 0)
        {
            _itemDal.Delete(item);
        }
        else
        {
            var variant = item.VariantId is { } id
                ? await _variantDal.GetAsync(v => v.Id == id, cancellationToken)
                : null;

            if (QuantityProblem(quantity, variant) is { } problem)
            {
                return problem;
            }

            item.Quantity = quantity;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Sepet güncellendi."));
    }

    public async Task<int> RevalueAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var items = await _itemDal.GetListAsync(i => i.CartId == cartId, cancellationToken);
        if (items.Count == 0)
        {
            return 0;
        }

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await _productDal.GetListAsync(p => productIds.Contains(p.Id), cancellationToken)).ToDictionary(p => p.Id);

        var changed = 0;
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || CurrentPrice(product) == item.UnitPrice)
            {
                continue;
            }

            var tracked = await _itemDal.GetTrackedAsync(i => i.Id == item.Id, cancellationToken);
            if (tracked is not null)
            {
                tracked.UnitPrice = CurrentPrice(product);
                changed++;
            }
        }

        if (changed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    /// <summary>Satır adedi 1..MaxQtyPerLine arasında; varyantlı üründe ayrıca stok kadar.</summary>
    private (HttpStatusCode, IResult)? QuantityProblem(int quantity, ProductVariant? variant)
    {
        if (quantity < 1)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Adet en az 1 olmalı."));
        }

        if (quantity > _shop.MaxQtyPerLine)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(
                $"Bir üründen en çok {_shop.MaxQtyPerLine} adet alabilirsiniz."));
        }

        if (variant is not null && variant.Stock < quantity)
        {
            return (HttpStatusCode.Conflict, new ErrorResult(variant.Stock == 0
                ? "Bu seçenek tükendi."
                : $"Bu seçenekten yalnız {variant.Stock} adet kaldı."));
        }

        return null;
    }

    private decimal CurrentPrice(Product product)
        => ProductRules.CampaignIsActive(product, _clock.GetUtcNow().UtcDateTime)
            ? product.CampaignPrice!.Value
            : product.Price;

    public async Task<int> CountAsync(Guid? cartId, CancellationToken cancellationToken = default)
    {
        if (cartId is not { } id)
        {
            return 0;
        }

        var items = await _itemDal.GetListAsync(i => i.CartId == id, cancellationToken);
        return items.Sum(i => i.Quantity);
    }
}
