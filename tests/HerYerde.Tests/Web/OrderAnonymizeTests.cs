using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>KAPANIŞ-5: kapanmış siparişte kişisel veri maskelenir ve denetim izi yazılır; açık siparişte 409.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderAnonymizeTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(OrderStatus.TeslimEdildi)]
    [InlineData(OrderStatus.IptalEdildi)]
    public async Task Kapanmis_sipariste_kisisel_veri_maskelenir_ve_denetim_satiri_yazilir(OrderStatus closed)
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context, closed);
        var client = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(
            client,
            $"/admin/orders/detail/{order.Id}",
            $"/admin/orders/anonymize/{order.Id}",
            new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var masked = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal("A*** Y***", masked.FullName);
        Assert.Equal("05*******82", masked.Phone);
        Assert.Equal("a***@***", masked.Email);
        Assert.Equal("***", masked.Address);
        Assert.Null(masked.Note);
        Assert.Equal(closed, masked.Status);
        Assert.Equal(order.Total, masked.Total);

        var audit = Assert.Single(await new EfAdminAuditLogDal(context).GetListAsync(a => a.Entity == "sipariş" && a.EntityId == order.Id));
        Assert.Equal("kişisel veri anonimleştirildi", audit.Action);
    }

    [Theory]
    [InlineData(OrderStatus.Beklemede)]
    [InlineData(OrderStatus.Onaylandi)]
    [InlineData(OrderStatus.Kargoda)]
    public async Task Acik_sipariste_anonimlestirme_409_ve_veri_degismez(OrderStatus open)
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context, open);
        var client = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(
            client,
            $"/admin/orders/detail/{order.Id}",
            $"/admin/orders/anonymize/{order.Id}",
            new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var unchanged = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal("Ayşe Yılmaz", unchanged.FullName);
        Assert.Equal("05424970982", unchanged.Phone);
        Assert.Empty(await new EfAdminAuditLogDal(context).GetListAsync(a => a.Entity == "sipariş" && a.EntityId == order.Id));
    }

    /// <summary>Beklemede açılan siparişi istenen duruma doğrudan çeker; akış kuralları burada sınanmıyor.</summary>
    private static async Task<Order> PlaceOrderAsync(HerYerdeContext context, OrderStatus status)
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);

        var (_, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz",
            "05424970982",
            "ayse@example.com",
            "Cumhuriyet Mah. 12/3",
            "İstanbul",
            "Kadıköy",
            "Kapıya bırakın",
            PaymentMethod.KapidaOdeme));

        var tracked = (await new EfOrderDal(context).GetTrackedAsync(o => o.Id == result.Data!.Id))!;
        tracked.Status = status;
        await new EfUnitOfWork(context).SaveChangesAsync();
        return tracked;
    }
}
