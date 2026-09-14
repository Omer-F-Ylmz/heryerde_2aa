using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using HerYerde.Tests.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class OutboxTests : IAsyncLifetime
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
    public async Task Siparis_sonrasi_kuyrukta_musteri_ve_magaza_kaydi_olusur()
    {
        await using var context = TestDb.NewContext();

        var order = await PlaceAsync(context, Draft);

        var queued = await new EfOutboxMessageDal(context).GetListAsync();
        Assert.Equal(2, queued.Count);
        Assert.Contains(queued, m => m.Type == OutboxType.OrderPlaced && m.To == Draft.Email);
        Assert.Contains(queued, m => m.Type == OutboxType.NewOrderForStore && m.To == TestData.StoreEmail);
        Assert.All(queued, m => Assert.Equal(OutboxStatus.Bekliyor, m.Status));
        Assert.All(queued, m => Assert.Contains(order.OrderNo, m.Body));
    }

    [Fact]
    public async Task E_postasiz_musteride_yalniz_magaza_kaydi_olusur()
    {
        await using var context = TestDb.NewContext();

        await PlaceAsync(context, Draft with { Email = null });

        var queued = await new EfOutboxMessageDal(context).GetListAsync();
        Assert.Single(queued);
        Assert.Equal(OutboxType.NewOrderForStore, queued[0].Type);
    }

    [Fact]
    public async Task Gonderim_hatasi_siparisi_bozmaz_deneme_sayisi_artar()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceAsync(context, Draft);
        var sender = new FakeNotificationSender { Fails = true };

        var result = await TestData.NewNotificationManager(context, sender).DispatchAsync();

        Assert.Equal(2, result.Failed);
        Assert.Equal(0, result.Sent);
        var queued = await new EfOutboxMessageDal(context).GetListAsync();
        Assert.All(queued, m => Assert.Equal(1, m.TryCount));
        Assert.All(queued, m => Assert.Equal(OutboxStatus.Bekliyor, m.Status));
        Assert.All(queued, m => Assert.NotNull(m.NextTryAt));
        // Sipariş kaydı gönderimden bağımsız durur.
        Assert.NotNull(await new EfOrderDal(context).GetAsync(o => o.Id == order.Id));
    }

    [Fact]
    public async Task Ucuncu_denemeden_sonra_kayit_basarisiz_olur()
    {
        await using var context = TestDb.NewContext();
        await PlaceAsync(context, Draft with { Email = null });
        var sender = new FakeNotificationSender { Fails = true };
        var clock = TestClock.Movable();
        var notifications = TestData.NewNotificationManager(context, sender, clock);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await notifications.DispatchAsync();
            clock.Advance(TimeSpan.FromDays(1));
        }

        var queued = Assert.Single(await new EfOutboxMessageDal(context).GetListAsync());
        Assert.Equal(3, queued.TryCount);
        Assert.Equal(OutboxStatus.Basarisiz, queued.Status);
        Assert.Null(queued.SentAt);
    }

    [Fact]
    public async Task Smtp_ayarsizken_gonderim_atlanir_kayit_kuyrukta_kalir()
    {
        await using var context = TestDb.NewContext();
        await PlaceAsync(context, Draft);
        var sender = new FakeNotificationSender { IsConfigured = false };

        var result = await TestData.NewNotificationManager(context, sender).DispatchAsync();

        Assert.Equal(2, result.Skipped);
        Assert.Equal(0, result.Sent);
        Assert.Empty(sender.Sent);
        var queued = await new EfOutboxMessageDal(context).GetListAsync();
        Assert.All(queued, m => Assert.Equal(OutboxStatus.Bekliyor, m.Status));
        Assert.All(queued, m => Assert.Equal(0, m.TryCount));
    }

    [Fact]
    public async Task Gonderilen_kayit_isaretlenir_ve_bir_daha_gonderilmez()
    {
        await using var context = TestDb.NewContext();
        await PlaceAsync(context, Draft);
        var sender = new FakeNotificationSender();
        var notifications = TestData.NewNotificationManager(context, sender);

        var first = await notifications.DispatchAsync();
        var second = await notifications.DispatchAsync();

        Assert.Equal(2, first.Sent);
        Assert.Equal(0, second.Sent);
        Assert.Equal(2, sender.Sent.Count);
        var queued = await new EfOutboxMessageDal(context).GetListAsync();
        Assert.All(queued, m => Assert.Equal(OutboxStatus.Gonderildi, m.Status));
        Assert.All(queued, m => Assert.Equal(TestClock.Now, m.SentAt));
    }

    /// <summary>KAPANIŞ-2 V-RET: gönderilmiş ya da vazgeçilmiş posta (alıcı, ad, token'lı bağlantı taşır) 30 gün sonra silinir;
    /// kuyrukta bekleyen kayda dokunulmaz.</summary>
    [Fact]
    public async Task Otuz_gunu_dolan_gonderilmis_ve_basarisiz_posta_silinir_bekleyen_kalir()
    {
        await using (var setup = TestDb.NewContext())
        {
            var dal = new EfOutboxMessageDal(setup);
            await dal.AddAsync(Mail(OutboxStatus.Gonderildi, sentAt: TestClock.Now.AddDays(-31)));
            await dal.AddAsync(Mail(OutboxStatus.Basarisiz, nextTryAt: TestClock.Now.AddDays(-31)));
            await dal.AddAsync(Mail(OutboxStatus.Gonderildi, sentAt: TestClock.Now.AddDays(-29)));
            await dal.AddAsync(Mail(OutboxStatus.Bekliyor, nextTryAt: TestClock.Now.AddDays(-60)));
            await new EfUnitOfWork(setup).SaveChangesAsync();
        }

        int removed;
        await using (var job = TestDb.NewContext())
        {
            removed = await TestData.NewNotificationManager(job).PurgeOlderThanAsync(TimeSpan.FromDays(30));
        }

        Assert.Equal(2, removed);
        await using var check = TestDb.NewContext();
        var left = await new EfOutboxMessageDal(check).GetListAsync();
        Assert.Equal(2, left.Count);
        Assert.Contains(left, m => m.Status == OutboxStatus.Bekliyor);
        Assert.Contains(left, m => m.SentAt == TestClock.Now.AddDays(-29));
    }

    [Fact]
    public void Kisisel_veri_temizligi_gece_isi_olarak_kayitlidir()
    {
        using var factory = new AdminWebFactory();

        var hosted = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name);

        Assert.Contains("PersonalDataCleanupHostedService", hosted);
    }

    private static OutboxMessage Mail(OutboxStatus status, DateTime? sentAt = null, DateTime? nextTryAt = null) => new()
    {
        Type = OutboxType.OrderPlaced,
        To = "ayse@example.com",
        Subject = "Siparişiniz alındı · HY-20260101-0001",
        Body = "Ayşe",
        Status = status,
        SentAt = sentAt,
        NextTryAt = nextTryAt
    };

    private static async Task<Order> PlaceAsync(HerYerdeContext context, OrderDraft draft)
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);

        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, draft);
        Assert.Equal(HttpStatusCode.Created, status);
        return result.Data!;
    }
}
