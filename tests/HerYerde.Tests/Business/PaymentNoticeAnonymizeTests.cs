using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Business;

/// <summary>D11 B4: havale bildirimindeki gönderen adı kişisel veridir; sipariş anonimleştirilince o da maskelenir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PaymentNoticeAnonymizeTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Anonimlestirme_bildirimdeki_gonderen_adini_maskeler()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", stock: 5);
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cart.AddAsync(cartId, productId, null, 1);
        var manager = TestData.NewOrderManager(context);
        var order = (await manager.PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", "0542 497 09 82", null, "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, PaymentMethod.HavaleEft))).Item2.Data!;

        var (submitted, _) = await manager.SubmitPaymentNoticeAsync(order.OrderNo, order.AccessToken,
            new PaymentNoticeDraft("Mehmet Yılmaz", new DateTime(2026, 1, 15), 529.90m, null));
        await manager.ChangeStatusAsync(order.Id, OrderStatus.IptalEdildi);
        // Yönetimdeki gibi ayrı istek: posta kayıtları bu bağlamda izlenmiyor.
        await using var admin = TestDb.NewContext();
        var (anonymized, _) = await TestData.NewOrderManager(admin).AnonymizeAsync(order.Id);

        Assert.Equal(HttpStatusCode.Created, submitted);
        Assert.Equal(HttpStatusCode.OK, anonymized);
        await using var check = TestDb.NewContext();
        Assert.Equal("M*** Y***", (await check.PaymentNotices.AsNoTracking().SingleAsync()).SenderName);
    }
}
