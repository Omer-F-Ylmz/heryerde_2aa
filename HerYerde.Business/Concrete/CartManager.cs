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

    public CartManager(
        ICartDal cartDal,
        ICartItemDal itemDal,
        IProductDal productDal,
        IProductVariantDal variantDal,
        IProductImageDal imageDal,
        IUnitOfWork unitOfWork,
        IOptions<ShopSettings> shop)
    {
        _cartDal = cartDal;
        _itemDal = itemDal;
        _productDal = productDal;
        _variantDal = variantDal;
        _imageDal = imageDal;
        _unitOfWork = unitOfWork;
        _shop = shop.Value;
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
        var variants = (await _variantDal.GetListAsync(v => productIds.Contains(v.ProductId), cancellationToken)).ToDictionary(v => v.Id);
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
        if (quantity < 1)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Adet en az 1 olmalı."));
        }

        if (await _cartDal.GetAsync(c => c.Id == cartId, cancellationToken) is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Sepet bulunamadı."));
        }

        var product = await _productDal.GetAsync(p => p.Id == productId && p.IsActive, cancellationToken);
        if (product is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Ürün bulunamadı."));
        }

        if (variantId is { } id && await _variantDal.GetAsync(v => v.Id == id && v.ProductId == productId, cancellationToken) is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Seçilen varyant bulunamadı."));
        }

        var existing = await _itemDal.GetTrackedAsync(
            i => i.CartId == cartId && i.ProductId == productId && i.VariantId == variantId,
            cancellationToken);

        if (existing is null)
        {
            await _itemDal.AddAsync(new CartItem
            {
                CartId = cartId,
                ProductId = productId,
                VariantId = variantId,
                Quantity = quantity,
                UnitPrice = ProductRules.CampaignIsActive(product, DateTime.UtcNow) ? product.CampaignPrice!.Value : product.Price
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
            item.Quantity = quantity;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Sepet güncellendi."));
    }

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
