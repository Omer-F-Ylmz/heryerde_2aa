using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D11 B1: WhatsApp/Instagram/telefon/mağaza siparişi yönetimden girilir; stok aynı işlemde düşer, kargo
/// kuraldan gelir ama yönetici üstüne yazabilir, kaynak kaydedilir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ManualOrderTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Manuel_siparis_varyant_ve_urun_stogunu_duser_kargo_kuraldan_gelir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m, stock: 5);
        var (_, variantId) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 4, price: 300m);

        var (status, placed) = await TestData.NewOrderManager(context).PlaceManualAsync(Draft(
            [new ManualOrderLine("celik-tencere", 2), new ManualOrderLine("SALVAR-M", 1)]));

        Assert.Equal(HttpStatusCode.Created, status);
        var order = placed.Data!;
        Assert.Equal(1200m, order.Subtotal);
        Assert.Equal(TestData.ShippingFee, order.ShippingFee);
        Assert.Equal(1200m + TestData.ShippingFee, order.Total);
        Assert.Equal(3, await TestData.ProductStockAsync(context, (await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere"))!.Id));
        Assert.Equal(3, (await new EfProductVariantDal(context).GetAsync(v => v.Id == variantId))!.Stock);
        Assert.Equal(2, (await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == order.Id)).Count);
    }

    [Fact]
    public async Task Stok_yetersizse_409_ve_hicbir_stok_dusmez_siparis_acilmaz()
    {
        await using var context = TestDb.NewContext();
        var pot = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", stock: 1);

        var (status, result) = await TestData.NewOrderManager(context).PlaceManualAsync(Draft(
            [new ManualOrderLine("celik-tencere", 2), new ManualOrderLine("cam-surahi", 2)]));

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("Cam Sürahi", result.Message);
        Assert.Equal(5, await TestData.ProductStockAsync(context, pot));
        Assert.Empty(await new EfOrderDal(context).GetListAsync());
    }

    [Fact]
    public async Task Yonetici_kargo_ucretinin_ustune_yazabilir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m);

        var (_, placed) = await TestData.NewOrderManager(context).PlaceManualAsync(
            Draft([new ManualOrderLine("celik-tencere", 1)]) with { ShippingFeeOverride = 0m });

        Assert.Equal(0m, placed.Data!.ShippingFee);
        Assert.Equal(450m, placed.Data!.Total);
    }

    [Fact]
    public async Task Kaynak_odeme_yontemi_kaydedilir_musteri_postasi_istege_bagli()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");

        var manager = TestData.NewOrderManager(context);
        var (_, silent) = await manager.PlaceManualAsync(Draft([new ManualOrderLine("celik-tencere", 1)]) with { Email = "ayse@example.com" });
        var (_, notified) = await manager.PlaceManualAsync(
            Draft([new ManualOrderLine("celik-tencere", 1)]) with { Email = "fatma@example.com", NotifyCustomer = true, Source = OrderSource.Magaza, PaymentMethod = PaymentMethod.NakitElden });

        var stored = (await new EfOrderDal(context).GetAsync(o => o.Id == notified.Data!.Id))!;
        Assert.Equal(OrderSource.WhatsApp, (await new EfOrderDal(context).GetAsync(o => o.Id == silent.Data!.Id))!.Source);
        Assert.Equal(OrderSource.Magaza, stored.Source);
        Assert.Equal(PaymentMethod.NakitElden, stored.PaymentMethod);
        var mails = await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.OrderPlaced);
        Assert.Equal("fatma@example.com", Assert.Single(mails).To);
    }

    private static ManualOrderDraft Draft(IReadOnlyList<ManualOrderLine> lines) => new(
        "Ayşe Yılmaz",
        "0542 497 09 82",
        null,
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        "WhatsApp'tan yazdı",
        PaymentMethod.KapidaOdeme,
        OrderSource.WhatsApp,
        lines,
        ShippingFeeOverride: null,
        NotifyCustomer: false);
}
