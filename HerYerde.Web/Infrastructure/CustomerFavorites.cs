using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

/// <summary>Sayfadaki kalplerin durumu: girişli üyenin favori ürün kimlikleri istek başına tek sorguyla okunur;
/// girişsiz ziyaretçide null (sorgu yok).</summary>
public static class CustomerFavorites
{
    private const string ItemKey = "heryerde.favoriler";

    public static async Task<HashSet<int>?> IdsAsync(HttpContext context, ICustomerService customers)
    {
        if (CustomerPolicy.CustomerId(context.User) is not { } customerId)
        {
            return null;
        }

        if (context.Items[ItemKey] is not HashSet<int> ids)
        {
            ids = await customers.GetFavoriteIdsAsync(customerId, context.RequestAborted);
            context.Items[ItemKey] = ids;
        }

        return ids;
    }
}
