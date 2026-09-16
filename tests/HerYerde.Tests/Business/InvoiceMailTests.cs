using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D14 A3: fatura yüklenince müşteriye token'lı indirme bağlantısıyla posta kuyruğa girer.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class InvoiceMailTests : IAsyncLifetime
{
    private static readonly DateTime InvoiceDate = new(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc);

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Fatura_kaydedilince_musteriye_indirme_baglantili_posta_kuyruga_girer()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var order = await PlaceAsync(context, "ayse@ornek.test");

        var (status, result) = await manager.SetInvoiceAsync(order.Id, "HY2026000123", InvoiceDate, "faturalar/abc.pdf");

        Assert.Equal(System.Net.HttpStatusCode.OK, status);
        Assert.Null(result.Data);
        var mail = Assert.Single(await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.InvoiceReady));
        Assert.Equal("ayse@ornek.test", mail.To);
        Assert.Equal($"Faturanız hazır · {order.OrderNo}", mail.Subject);
        Assert.Contains($"{TestData.BaseUrl}/siparis/{order.OrderNo}/fatura?t={order.AccessToken}", mail.Body);
        Assert.Contains("HY2026000123", mail.Body);
        Assert.Equal(OutboxStatus.Bekliyor, mail.Status);
    }

    [Fact]
    public async Task E_postasi_olmayan_siparisde_fatura_postasi_yok()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceAsync(context, null);

        await TestData.NewOrderManager(context).SetInvoiceAsync(order.Id, "HY2026000124", InvoiceDate, "faturalar/def.pdf");

        Assert.Empty(await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.InvoiceReady));
        Assert.Equal("HY2026000124", (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.InvoiceNo);
    }

    private static async Task<Order> PlaceAsync(HerYerdeContext context, string? email)
    {
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var cartManager = TestData.NewCartManager(context);
        var (_, cart) = await cartManager.GetOrCreateAsync(null);
        await cartManager.AddAsync(cart.Data!.Id, productId, null, 1);

        var (_, placed) = await TestData.NewOrderManager(context).PlaceAsync(cart.Data.Id, new OrderDraft(
            "Ayşe Yılmaz",
            "0542 497 09 82",
            email,
            "Cumhuriyet Mah. 12/3",
            "İstanbul",
            "Kadıköy",
            null,
            PaymentMethod.KapidaOdeme));
        return placed.Data!;
    }
}
