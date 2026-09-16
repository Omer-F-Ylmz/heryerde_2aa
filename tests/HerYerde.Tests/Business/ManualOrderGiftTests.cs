using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D14 A4: vitrindeki "1 alana 1 hediye" kampanyası yönetimden girilen siparişe de uygulanır;
/// kutu kapatılırsa hiç uygulanmaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ManualOrderGiftTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Manuel_siparise_hediye_satiri_eklenir_ve_hediye_stogu_duser()
    {
        await using var context = TestDb.NewContext();
        var potId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m, stock: 5);
        var giftId = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", price: 120m, stock: 4);
        await TestData.SetGiftAsync(context, potId, GiftMode.BaskaUrun, giftId);

        var (status, placed) = await TestData.NewOrderManager(context).PlaceManualAsync(Draft(applyGifts: true));

        Assert.Equal(HttpStatusCode.Created, status);
        var items = await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == placed.Data!.Id);
        var gift = Assert.Single(items, i => i.IsGift);
        Assert.Equal("Cam Sürahi", gift.ProductName);
        Assert.Equal(2, gift.Quantity);
        Assert.Equal(0m, gift.UnitPrice);
        // Hediye satırı toplamı büyütmez; stok düşer.
        Assert.Equal(900m, placed.Data!.Subtotal);
        Assert.Equal(2, await TestData.ProductStockAsync(context, giftId));
    }

    [Fact]
    public async Task Kutu_kapaliyken_hediye_uygulanmaz()
    {
        await using var context = TestDb.NewContext();
        var potId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m, stock: 5);
        var giftId = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", price: 120m, stock: 4);
        await TestData.SetGiftAsync(context, potId, GiftMode.BaskaUrun, giftId);

        var (_, placed) = await TestData.NewOrderManager(context).PlaceManualAsync(Draft(applyGifts: false));

        var items = await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == placed.Data!.Id);
        Assert.DoesNotContain(items, i => i.IsGift);
        Assert.Equal(4, await TestData.ProductStockAsync(context, giftId));
    }

    private static ManualOrderDraft Draft(bool applyGifts) => new(
        "Ayşe Yılmaz",
        "0542 497 09 82",
        null,
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        null,
        PaymentMethod.KapidaOdeme,
        OrderSource.WhatsApp,
        [new ManualOrderLine("celik-tencere", 2)],
        ShippingFeeOverride: null,
        NotifyCustomer: false,
        ConsentConfirmed: true,
        ApplyGifts: applyGifts);
}
