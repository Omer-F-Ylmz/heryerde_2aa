using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class OrderShippingTests : IAsyncLifetime
{
    private static readonly OrderDraft Draft = new(
        "Ayşe Yılmaz",
        "0542 497 09 82",
        "ayse@example.com",
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        null,
        PaymentMethod.KapidaOdeme);

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Kargoda_gecisi_takip_bilgisi_olmadan_reddedilir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var order = await PlaceAsync(context);
        await manager.ChangeStatusAsync(order.Id, OrderStatus.Onaylandi);
        await manager.ChangeStatusAsync(order.Id, OrderStatus.Hazirlaniyor);

        var (missingBoth, both) = await manager.ChangeStatusAsync(order.Id, OrderStatus.Kargoda);
        var (missingNo, _) = await manager.ChangeStatusAsync(order.Id, OrderStatus.Kargoda, TestData.Carrier);

        Assert.Equal(HttpStatusCode.BadRequest, missingBoth);
        Assert.Equal(HttpStatusCode.BadRequest, missingNo);
        Assert.Contains("takip", both.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OrderStatus.Hazirlaniyor, (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.Status);
    }

    [Fact]
    public async Task Takip_bilgisi_girilince_kargo_postasi_kuyruga_girer()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var order = await PlaceAsync(context);
        await manager.ChangeStatusAsync(order.Id, OrderStatus.Onaylandi);
        await manager.ChangeStatusAsync(order.Id, OrderStatus.Hazirlaniyor);

        var (status, _) = await manager.ChangeStatusAsync(order.Id, OrderStatus.Kargoda, TestData.Carrier, "1234567890");

        Assert.Equal(HttpStatusCode.OK, status);
        var saved = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal(OrderStatus.Kargoda, saved.Status);
        Assert.Equal(TestData.Carrier, saved.Carrier);
        Assert.Equal("1234567890", saved.TrackingNo);

        var shipped = Assert.Single(
            await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.OrderShipped));
        Assert.Equal(Draft.Email, shipped.To);
        Assert.Contains("1234567890", shipped.Body);
        Assert.Contains(TestData.Carrier, shipped.Body);
    }

    [Fact]
    public async Task Okunmamis_siparis_ilk_detay_acilisinda_gorulmus_olur()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var order = await PlaceAsync(context);

        Assert.Equal(1, (await manager.UnseenCountAsync()).Item2.Data);
        Assert.Null((await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.SeenAt);

        await manager.MarkSeenAsync(order.Id);
        var seenAt = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.SeenAt;

        Assert.Equal(TestClock.Now, seenAt);
        Assert.Equal(0, (await manager.UnseenCountAsync()).Item2.Data);
    }

    private static async Task<Order> PlaceAsync(HerYerdeContext context)
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);

        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft);
        Assert.Equal(HttpStatusCode.Created, status);
        return result.Data!;
    }
}
