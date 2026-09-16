using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D14 B5: teslimden 7 gün sonra "ürünlerinizi değerlendirin" postası; tek kez, e-postası olana.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReviewMailTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Yedinci_gunde_tek_kez_degerlendirme_postasi_cikar()
    {
        await using var context = TestDb.NewContext();
        var clock = TestClock.Movable();
        var order = await DeliveredAsync(context, "ayse@ornek.test", clock);
        var manager = TestData.NewNotificationManager(context, clock: clock);

        // 6. günde henüz sırası gelmemiş.
        clock.Advance(TimeSpan.FromDays(6));
        Assert.Equal(0, await manager.QueueDueReviewInvitesAsync());
        Assert.Empty(await Mails(context));

        clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(1, await manager.QueueDueReviewInvitesAsync());
        // İkinci tarama aynı siparişi bir daha almaz.
        Assert.Equal(0, await manager.QueueDueReviewInvitesAsync());

        var mail = Assert.Single(await Mails(context));
        Assert.Equal("ayse@ornek.test", mail.To);
        Assert.Contains($"Ürününüz nasıl? · {order.OrderNo}", mail.Subject);
        Assert.Contains($"/urun/celik-tencere?siparis={order.OrderNo}", mail.Body);
        Assert.NotNull((await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.ReviewMailAt);
    }

    [Fact]
    public async Task E_postasi_olmayan_siparis_atlanir_ama_yeniden_denenmez()
    {
        await using var context = TestDb.NewContext();
        var clock = TestClock.Movable();
        var order = await DeliveredAsync(context, null, clock);
        clock.Advance(TimeSpan.FromDays(8));

        var queued = await TestData.NewNotificationManager(context, clock: clock).QueueDueReviewInvitesAsync();

        Assert.Equal(0, queued);
        Assert.Empty(await Mails(context));
        // İşaret yine konur: her gece yeniden taranmasın.
        Assert.NotNull((await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.ReviewMailAt);
    }

    [Fact]
    public async Task Iptal_edilmis_siparise_davet_gitmez()
    {
        await using var context = TestDb.NewContext();
        var clock = TestClock.Movable();
        var order = await DeliveredAsync(context, "ayse@ornek.test", clock);
        var tracked = (await new EfOrderDal(context).GetTrackedAsync(o => o.Id == order.Id))!;
        tracked.Status = OrderStatus.IptalEdildi;
        await new EfUnitOfWork(context).SaveChangesAsync();
        clock.Advance(TimeSpan.FromDays(8));

        Assert.Equal(0, await TestData.NewNotificationManager(context, clock: clock).QueueDueReviewInvitesAsync());
        Assert.Empty(await Mails(context));
    }

    private static Task<List<OutboxMessage>> Mails(HerYerdeContext context)
        => new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.ReviewInvite);

    private static async Task<Order> DeliveredAsync(HerYerdeContext context, string? email, TimeProvider clock)
    {
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var cartManager = TestData.NewCartManager(context, clock);
        var (_, cart) = await cartManager.GetOrCreateAsync(null);
        await cartManager.AddAsync(cart.Data!.Id, productId, null, 1);

        var manager = TestData.NewOrderManager(context, clock);
        var (_, placed) = await manager.PlaceAsync(cart.Data.Id, new OrderDraft(
            "Ayşe Yılmaz",
            "0542 497 09 82",
            email,
            "Cumhuriyet Mah. 12/3",
            "İstanbul",
            "Kadıköy",
            null,
            PaymentMethod.KapidaOdeme));
        var order = placed.Data!;

        await manager.ChangeStatusAsync(order.Id, OrderStatus.Onaylandi);
        await manager.ChangeStatusAsync(order.Id, OrderStatus.Hazirlaniyor);
        await manager.ChangeStatusAsync(order.Id, OrderStatus.Kargoda, TestData.Carrier, "1234567890");
        await manager.ChangeStatusAsync(order.Id, OrderStatus.TeslimEdildi);
        return order;
    }
}
