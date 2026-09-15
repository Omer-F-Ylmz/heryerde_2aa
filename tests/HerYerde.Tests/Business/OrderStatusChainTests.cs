using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D10 A3: Onaylandı → Hazırlanıyor → Kargoda; aşama atlanmaz, geri dönülmez.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderStatusChainTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Hazirlaniyor_onaylandi_ile_kargoda_arasinda_durur()
    {
        Assert.True(OrderRules.CanTransition(OrderStatus.Onaylandi, OrderStatus.Hazirlaniyor));
        Assert.True(OrderRules.CanTransition(OrderStatus.Hazirlaniyor, OrderStatus.Kargoda));
        Assert.False(OrderRules.CanTransition(OrderStatus.Onaylandi, OrderStatus.Kargoda));
        Assert.False(OrderRules.CanTransition(OrderStatus.Hazirlaniyor, OrderStatus.Onaylandi));
        Assert.False(OrderRules.CanTransition(OrderStatus.Hazirlaniyor, OrderStatus.IptalEdildi));
    }

    [Fact]
    public async Task Siparis_hazirlaniyora_ilerler_geri_donus_400()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cart.AddAsync(cartId, productId, null, 1);
        var manager = TestData.NewOrderManager(context);
        var order = (await manager.PlaceAsync(cartId, new OrderDraft("Ayşe Yılmaz", "05001234567", null, "Örnek mah. 1", "İstanbul", "Kadıköy", null, PaymentMethod.KapidaOdeme))).Item2.Data!;

        await manager.ChangeStatusAsync(order.Id, OrderStatus.Onaylandi);
        var (skip, _) = await manager.ChangeStatusAsync(order.Id, OrderStatus.Kargoda, TestData.Carrier, "123");
        var (prepared, _) = await manager.ChangeStatusAsync(order.Id, OrderStatus.Hazirlaniyor);
        var (back, _) = await manager.ChangeStatusAsync(order.Id, OrderStatus.Onaylandi);
        var (shipped, _) = await manager.ChangeStatusAsync(order.Id, OrderStatus.Kargoda, TestData.Carrier, "1234567890");

        Assert.Equal(HttpStatusCode.BadRequest, skip);
        Assert.Equal(HttpStatusCode.OK, prepared);
        Assert.Equal(HttpStatusCode.BadRequest, back);
        Assert.Equal(HttpStatusCode.OK, shipped);
        Assert.Equal(OrderStatus.Kargoda, (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.Status);
    }
}
