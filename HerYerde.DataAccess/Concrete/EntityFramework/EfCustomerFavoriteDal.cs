using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfCustomerFavoriteDal : EfEntityRepositoryBase<CustomerFavorite, HerYerdeContext>, ICustomerFavoriteDal
{
    public EfCustomerFavoriteDal(HerYerdeContext context) : base(context)
    {
    }

    public async Task<HashSet<int>> ProductIdsAsync(int customerId, CancellationToken cancellationToken = default)
        => (await Context.CustomerFavorites.AsNoTracking()
                .Where(f => f.CustomerId == customerId)
                .Select(f => f.ProductId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
}
