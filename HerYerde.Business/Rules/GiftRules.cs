using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Rules;

/// <summary>Sepetteki bir satırın doğurduğu hediye. Available=false ise stok kalmadığı için verilemez.</summary>
public sealed record GiftPlan(int ProductId, string ProductName, string Sku, int Quantity, bool Available);

/// <summary>"1 alana 1 hediye": sepet satırlarından hediye listesini üretir. DB'siz, saf fonksiyon.</summary>
public static class GiftRules
{
    public static string Note(IEnumerable<GiftPlan> plans)
    {
        var missing = plans.Where(p => !p.Available).Select(p => p.ProductName).Distinct().ToList();
        return missing.Count == 0
            ? string.Empty
            : $"Hediye stoğu kalmadığı için {string.Join(", ", missing)} siparişe eklenemedi.";
    }

    /// <summary>Her sepet satırı için ürünün hediye ayarını çözer; aynı hediye ürünü birden çok
    /// satırdan geliyorsa tek satırda toplanır ve stok toplam adede göre sınanır.</summary>
    public static List<GiftPlan> Plan(
        IEnumerable<CartItem> items,
        IReadOnlyDictionary<int, Product> products,
        DateTime now)
    {
        var wanted = new Dictionary<int, int>();
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !IsActive(product, now))
            {
                continue;
            }

            var giftId = product.GiftMode == GiftMode.AyniUrun ? product.Id : product.GiftProductId!.Value;
            wanted[giftId] = wanted.GetValueOrDefault(giftId) + product.GiftQty * item.Quantity;
        }

        var plans = new List<GiftPlan>();
        foreach (var (giftId, quantity) in wanted.OrderBy(p => p.Key))
        {
            if (!products.TryGetValue(giftId, out var gift))
            {
                continue;
            }

            // Hediye ürünü stok tutmuyorsa (NULL) sınırsız; tutuyorsa sepetteki satış adedi de aynı stoktan düşer.
            var available = gift.Stock is not { } stock || stock >= quantity + SoldQuantity(items, giftId);
            plans.Add(new GiftPlan(gift.Id, gift.Name, gift.Slug, quantity, available));
        }

        return plans;
    }

    /// <summary>Hediye yalnız kampanya süresi içinde geçerlidir; bitiş boşsa süresizdir.</summary>
    public static bool IsActive(Product product, DateTime now)
        => product.GiftMode != GiftMode.Yok
           && (product.CampaignEndsAt is null || product.CampaignEndsAt > now)
           && (product.GiftMode == GiftMode.AyniUrun || product.GiftProductId is not null);

    private static int SoldQuantity(IEnumerable<CartItem> items, int productId)
        => items.Where(i => i.ProductId == productId).Sum(i => i.Quantity);
}
