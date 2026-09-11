using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HerYerde.Tests.Business;

/// <summary>ZAP 90022: aynı sepet satırına eş zamanlı iki form (azalt + sil) 500 değil "satır bulunamadı" alır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CartConcurrencyTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Satir_kayit_aninda_baska_istekle_silinmisse_404_doner(int quantity)
    {
        Guid cartId;
        int itemId;
        await using (var context = TestDb.NewContext())
        {
            var manager = TestData.NewCartManager(context);
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;
            await manager.AddAsync(cartId, productId, variantId: null, quantity: 1);
            itemId = (await manager.GetAsync(cartId)).Item2.Data!.Lines[0].ItemId;
        }

        var options = new DbContextOptionsBuilder<HerYerdeContext>()
            .UseSqlServer(TestDb.ConnectionString)
            .AddInterceptors(new DeleteBeforeSave(itemId))
            .Options;
        await using var racing = new HerYerdeContext(options);

        var (status, _) = await TestData.NewCartManager(racing).SetQuantityAsync(cartId, itemId, quantity);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    /// <summary>Satır okunduktan sonra, kayıttan hemen önce ikinci isteğin silmesini taklit eder.</summary>
    private sealed class DeleteBeforeSave(int itemId) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await using var other = TestDb.NewContext();
            await other.CartItems.Where(i => i.Id == itemId).ExecuteDeleteAsync(cancellationToken);
            return result;
        }
    }
}
