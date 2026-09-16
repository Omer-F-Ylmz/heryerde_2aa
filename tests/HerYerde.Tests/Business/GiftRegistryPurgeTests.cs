using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Business;

/// <summary>D15 A2 / veri envanteri: çeyiz listesi sahip verisi etkinlik tarihinden bir yıl sonra silinir;
/// listeye bağlı sepet satırları da gider, verilmiş siparişin satırı kalır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class GiftRegistryPurgeTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Etkinligin_uzerinden_bir_yil_gecen_liste_sepet_satirlariyla_silinir()
    {
        int expiredId;
        int keptId;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            (expiredId, _, _) = await TestData.AddGiftRegistryAsync(context, eventDate: TestClock.Now.Date.AddDays(-366));
            (keptId, _, _) = await TestData.AddGiftRegistryAsync(context, phone: "05329998877", eventDate: TestClock.Now.Date.AddDays(-364));
            var expiredItem = await TestData.AddGiftRegistryItemAsync(context, expiredId, productId);
            await TestData.AddGiftRegistryItemAsync(context, keptId, productId);

            var cart = new Cart { Id = Guid.NewGuid(), CreatedAt = TestClock.Now };
            await new EfCartDal(context).AddAsync(cart);
            await new EfCartItemDal(context).AddAsync(new CartItem
            {
                CartId = cart.Id,
                ProductId = productId,
                Quantity = 1,
                UnitPrice = 450m,
                GiftRegistryItemId = expiredItem
            });
            await new EfUnitOfWork(context).SaveChangesAsync();
        }

        int purged;
        await using (var job = TestDb.NewContext())
        {
            purged = await TestData.NewGiftRegistryManager(job).PurgeOlderThanAsync(TimeSpan.FromDays(365));
        }

        Assert.Equal(1, purged);
        await using var check = TestDb.NewContext();
        Assert.Equal(keptId, (await check.GiftRegistries.SingleAsync()).Id);
        Assert.Equal(keptId, (await check.GiftRegistryItems.SingleAsync()).GiftRegistryId);
        Assert.Empty(await check.CartItems.ToListAsync());
    }
}
