using HerYerde.Business.Concrete;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Business;

/// <summary>D12 C4: ciro yalnız parası alınmış siparişten (kart başarılı, havale onaylı, kapıda/nakit teslim edilmiş);
/// çok satanlar adede göre; kaynak dağılımı iptal dışı siparişlerden.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SalesReportTests : IAsyncLifetime
{
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ciro_yalniz_odenmis_ve_teslim_edilmis_siparisleri_sayar()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 100m);
        var delivered = await PlaceAsync(context, PaymentMethod.KapidaOdeme, OrderStatus.TeslimEdildi);
        await PlaceAsync(context, PaymentMethod.KapidaOdeme, OrderStatus.Kargoda);
        var transfer = await PlaceAsync(context, PaymentMethod.HavaleEft, OrderStatus.Onaylandi);
        await PlaceAsync(context, PaymentMethod.HavaleEft, OrderStatus.Beklemede);
        await PlaceAsync(context, PaymentMethod.KapidaOdeme, OrderStatus.IptalEdildi);
        var card = await PlaceAsync(context, PaymentMethod.KrediKarti, OrderStatus.Beklemede, PaymentStatus.Basarili);
        await PlaceAsync(context, PaymentMethod.KrediKarti, OrderStatus.IptalEdildi, PaymentStatus.Iade);

        var report = await NewManager(context).BuildAsync(From, To, ReportPeriod.Ay, TimeZoneInfo.Utc);

        var expected = delivered.Total + transfer.Total + card.Total;
        Assert.Equal(expected, report.Revenue);
        Assert.Equal(7, report.OrderCount);
        Assert.Equal(3, report.PaidOrderCount);
        Assert.Equal(Math.Round(expected / 3, 2), report.AverageBasket);
        Assert.Equal(Math.Round(2m / 7, 4), report.CancelRate);
        Assert.Equal(expected, Assert.Single(report.Periods).Revenue);
    }

    [Fact]
    public async Task Cok_satanlar_adede_gore_siralanir_ilk_on()
    {
        await using var context = TestDb.NewContext();
        var slugs = Enumerable.Range(1, 11).Select(i => $"urun-{i:00}").ToList();
        foreach (var slug in slugs)
        {
            await TestData.AddHomeProductAsync(context, "Ürün " + slug[^2..], slug, price: 10m);
        }

        var manager = TestData.NewOrderManager(context);
        for (var i = 0; i < slugs.Count; i++)
        {
            await manager.PlaceManualAsync(Draft(PaymentMethod.KapidaOdeme, OrderSource.WhatsApp, new ManualOrderLine(slugs[i], i + 1)));
        }

        var report = await NewManager(context).BuildAsync(From, To, ReportPeriod.Gun, TimeZoneInfo.Utc);

        Assert.Equal(10, report.TopProducts.Count);
        Assert.Equal(["Ürün 11", "Ürün 10", "Ürün 09"], report.TopProducts.Take(3).Select(p => p.ProductName));
        Assert.Equal(11, report.TopProducts[0].Quantity);
        Assert.DoesNotContain(report.TopProducts, p => p.ProductName == "Ürün 01");
    }

    [Fact]
    public async Task Cok_satanlarda_ad_adet_tutar_esitse_stok_koduna_gore_sirali()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 100m);
        var order = await PlaceAsync(context, PaymentMethod.KapidaOdeme, OrderStatus.Beklemede);
        context.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductName = "Şalvar", Sku = "SALVAR-M", Quantity = 5, UnitPrice = 10m });
        context.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductName = "Şalvar", Sku = "SALVAR-L", Quantity = 5, UnitPrice = 10m });
        await context.SaveChangesAsync();

        var report = await NewManager(context).BuildAsync(From, To, ReportPeriod.Ay, TimeZoneInfo.Utc);

        Assert.Equal(["SALVAR-L", "SALVAR-M"], report.TopProducts.Take(2).Select(p => p.Sku));
    }

    [Fact]
    public async Task Kaynak_dagilimi_iptal_disi_siparisleri_sayar()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 100m);
        var manager = TestData.NewOrderManager(context);
        await manager.PlaceManualAsync(Draft(PaymentMethod.KapidaOdeme, OrderSource.WhatsApp, new ManualOrderLine("celik-tencere", 1)));
        await manager.PlaceManualAsync(Draft(PaymentMethod.KapidaOdeme, OrderSource.WhatsApp, new ManualOrderLine("celik-tencere", 1)));
        var cancelled = (await manager.PlaceManualAsync(Draft(PaymentMethod.KapidaOdeme, OrderSource.Instagram, new ManualOrderLine("celik-tencere", 1)))).Item2.Data!;
        await manager.PlaceManualAsync(Draft(PaymentMethod.NakitElden, OrderSource.Magaza, new ManualOrderLine("celik-tencere", 1)));
        (await context.Orders.SingleAsync(o => o.Id == cancelled.Id)).Status = OrderStatus.IptalEdildi;
        await context.SaveChangesAsync();

        var report = await NewManager(context).BuildAsync(From, To, ReportPeriod.Hafta, TimeZoneInfo.Utc);

        Assert.Equal(2, report.Sources.Single(s => s.Source == OrderSource.WhatsApp).Orders);
        Assert.Equal(1, report.Sources.Single(s => s.Source == OrderSource.Magaza).Orders);
        Assert.DoesNotContain(report.Sources, s => s.Source == OrderSource.Instagram);
        Assert.Equal(1, report.PaymentMethods.Single(m => m.Method == PaymentMethod.NakitElden).Orders);
    }

    private static ReportManager NewManager(HerYerde.DataAccess.Concrete.EntityFramework.Contexts.HerYerdeContext context)
        => new(new EfOrderDal(context), new EfOrderItemDal(context), new EfPaymentDal(context));

    private static async Task<Order> PlaceAsync(
        HerYerde.DataAccess.Concrete.EntityFramework.Contexts.HerYerdeContext context,
        PaymentMethod method,
        OrderStatus status,
        PaymentStatus? payment = null)
    {
        var placed = (await TestData.NewOrderManager(context).PlaceManualAsync(
            Draft(method == PaymentMethod.KrediKarti ? PaymentMethod.KapidaOdeme : method, OrderSource.Telefon,
                new ManualOrderLine("celik-tencere", 1)))).Item2.Data!;
        // Kart siparişi yönetimden açılmaz; ödeme kaydıyla birlikte sonradan karta çevrilir.
        var order = await context.Orders.SingleAsync(o => o.Id == placed.Id);
        order.Status = status;
        order.PaymentMethod = method;
        if (payment is { } paymentStatus)
        {
            context.Payments.Add(new Payment
            {
                OrderId = order.Id,
                Provider = "iyzico",
                ConversationId = Guid.NewGuid().ToString("N"),
                Status = paymentStatus,
                Amount = order.Total,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.CreatedAt
            });
        }

        await context.SaveChangesAsync();
        return order;
    }

    private static ManualOrderDraft Draft(PaymentMethod method, OrderSource source, ManualOrderLine line) => new(
        "Ayşe Yılmaz", "0542 497 09 82", null, "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null,
        method, source, [line], ShippingFeeOverride: null, NotifyCustomer: false, ConsentConfirmed: true);
}
