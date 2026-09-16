using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfGiftRegistryItemDal : EfEntityRepositoryBase<GiftRegistryItem, HerYerdeContext>, IGiftRegistryItemDal
{
    public EfGiftRegistryItemDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<int> IncrementReceivedAsync(int itemId, int quantity, CancellationToken cancellationToken = default)
        => Context.GiftRegistryItems
            .Where(i => i.Id == itemId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ReceivedQty, i => i.ReceivedQty + quantity), cancellationToken);
}
