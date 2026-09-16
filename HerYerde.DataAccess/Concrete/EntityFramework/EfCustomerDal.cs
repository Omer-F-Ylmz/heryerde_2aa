using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfCustomerDal : EfEntityRepositoryBase<Customer, HerYerdeContext>, ICustomerDal
{
    public EfCustomerDal(HerYerdeContext context) : base(context)
    {
    }

    public async Task<int> LinkGuestRecordsAsync(int customerId, string email, CancellationToken cancellationToken = default)
    {
        var linked = await Context.Orders
            .Where(o => o.CustomerId == null && o.Email != null && o.Email.ToLower() == email)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.CustomerId, customerId), cancellationToken);
        await Context.GiftRegistries
            .Where(r => r.CustomerId == null && r.Email != null && r.Email.ToLower() == email)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CustomerId, customerId), cancellationToken);
        return linked;
    }
}
