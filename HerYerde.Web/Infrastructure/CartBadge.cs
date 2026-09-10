using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

/// <summary>Başlıktaki sepet rozeti: çerez yoksa sorgu atılmaz, varsa istek başına bir kez sorulur.</summary>
public static class CartBadge
{
    private const string ItemKey = "heryerde:cart-count";

    public static async Task<int> CountAsync(HttpContext context, ICartService cartService, CancellationToken cancellationToken = default)
    {
        if (context.Items.TryGetValue(ItemKey, out var cached) && cached is int count)
        {
            return count;
        }

        var fresh = await cartService.CountAsync(CartCookie.Read(context), cancellationToken);
        context.Items[ItemKey] = fresh;
        return fresh;
    }
}
