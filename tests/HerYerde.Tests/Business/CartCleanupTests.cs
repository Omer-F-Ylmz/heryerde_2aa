using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

/// <summary>G13/S13: dokunulmamış anonim sepetler süresiz birikmemeli.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CartCleanupTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Otuz_bir_gunluk_sepet_silinir_yirmi_dokuz_gunluk_kalir()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
        var eski = await AddCartAsync(context, productId, TestClock.Now.AddDays(-31));
        var yeni = await AddCartAsync(context, productId, TestClock.Now.AddDays(-29));

        // Temizlik işi kendi kapsamında çalışır; bu yüzden ayrı bir bağlamdan çağrılıyor.
        int silinen;
        await using (var job = TestDb.NewContext())
        {
            silinen = await TestData.NewCartManager(job, TestClock.Fixed).PurgeStaleAsync(TimeSpan.FromDays(30));
        }

        Assert.Equal(1, silinen);
        await using var check = TestDb.NewContext();
        var carts = await new EfCartDal(check).GetListAsync();
        Assert.Equal(yeni, Assert.Single(carts).Id);
        Assert.Empty(await new EfCartItemDal(check).GetListAsync(i => i.CartId == eski));
        Assert.Single(await new EfCartItemDal(check).GetListAsync(i => i.CartId == yeni));
    }

    [Fact]
    public async Task Temizlikte_silinecek_sepet_yoksa_sifir_doner()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
        await AddCartAsync(context, productId, TestClock.Now.AddDays(-1));

        await using var job = TestDb.NewContext();
        Assert.Equal(0, await TestData.NewCartManager(job, TestClock.Fixed).PurgeStaleAsync(TimeSpan.FromDays(30)));
    }

    private static async Task<Guid> AddCartAsync(HerYerde.DataAccess.Concrete.EntityFramework.Contexts.HerYerdeContext context, int productId, DateTime createdAt)
    {
        var cart = new Cart { Id = Guid.NewGuid(), CreatedAt = createdAt };
        await new EfCartDal(context).AddAsync(cart);
        await new EfCartItemDal(context).AddAsync(new CartItem
        {
            CartId = cart.Id,
            ProductId = productId,
            Quantity = 1,
            UnitPrice = 450m
        });
        await new EfUnitOfWork(context).SaveChangesAsync();
        return cart.Id;
    }
}
