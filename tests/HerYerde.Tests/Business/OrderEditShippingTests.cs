using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D14 A4: sipariş düzenlemede kargo ücreti yeni ara toplama göre yeniden hesaplanır; yönetici elle
/// yazdıysa dokunulmaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderEditShippingTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Adet_artinca_kargo_yeniden_hesaplanir_bedava_esigi_uygulanir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 900m, stock: 10);

        var (_, placed) = await manager.PlaceManualAsync(Draft(2, shippingOverride: null));
        var order = placed.Data!;
        Assert.Equal(TestData.ShippingFee, order.ShippingFee);

        var item = (await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == order.Id)).Single();
        var (status, detail) = await manager.EditAsync(order.Id, Edit(order.Id, item.Id, 3));

        var saved = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2700m, saved.Subtotal);
        Assert.Equal(0m, saved.ShippingFee);
        Assert.Equal(2700m, saved.Total);
        Assert.Contains("kargo", detail.Data);
    }

    [Fact]
    public async Task Adet_azalinca_kargo_geri_gelir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 900m, stock: 10);

        var (_, placed) = await manager.PlaceManualAsync(Draft(3, shippingOverride: null));
        var order = placed.Data!;
        Assert.Equal(0m, order.ShippingFee);

        var item = (await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == order.Id)).Single();
        await manager.EditAsync(order.Id, Edit(order.Id, item.Id, 1));

        var saved = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal(900m, saved.Subtotal);
        Assert.Equal(TestData.ShippingFee, saved.ShippingFee);
        Assert.Equal(900m + TestData.ShippingFee, saved.Total);
    }

    [Fact]
    public async Task Yonetici_ustune_yazdiysa_kargo_korunur()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 900m, stock: 10);

        var (_, placed) = await manager.PlaceManualAsync(Draft(2, shippingOverride: 25m));
        var order = placed.Data!;
        Assert.True(order.ShippingOverridden);

        var item = (await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == order.Id)).Single();
        await manager.EditAsync(order.Id, Edit(order.Id, item.Id, 3));

        var saved = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal(2700m, saved.Subtotal);
        Assert.Equal(25m, saved.ShippingFee);
        Assert.Equal(2725m, saved.Total);
    }

    private static OrderEdit Edit(int orderId, int itemId, int quantity) => new(
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        "0542 497 09 82",
        null,
        new Dictionary<int, int> { [itemId] = quantity });

    private static ManualOrderDraft Draft(int quantity, decimal? shippingOverride) => new(
        "Ayşe Yılmaz",
        "0542 497 09 82",
        null,
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        null,
        PaymentMethod.KapidaOdeme,
        OrderSource.WhatsApp,
        [new ManualOrderLine("celik-tencere", quantity)],
        shippingOverride,
        NotifyCustomer: false,
        ConsentConfirmed: true);
}
