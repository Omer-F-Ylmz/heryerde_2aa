using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

[Collection(DatabaseCollection.Name)]
public sealed class AdminOrderFlowTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Yonetici_siparisi_listede_gorur_ve_durumu_sirayla_ilerletir()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context);
        var client = await _factory.CreateSignedInClientAsync();

        var list = await client.GetAsync("/admin/orders");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(order.OrderNo, await list.Content.ReadAsStringAsync());

        var response = await HtmlForm.PostAsync(
            client,
            $"/admin/orders/detail/{order.Id}",
            $"/admin/orders/changestatus/{order.Id}",
            new Dictionary<string, string> { ["next"] = ((int)OrderStatus.Onaylandi).ToString() });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(OrderStatus.Onaylandi, (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.Status);
    }

    [Fact]
    public async Task Geri_donen_durum_degisimi_400_ile_reddedilir()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceOrderAsync(context);
        var tracked = (await new EfOrderDal(context).GetTrackedAsync(o => o.Id == order.Id))!;
        tracked.Status = OrderStatus.Kargoda;
        await new EfUnitOfWork(context).SaveChangesAsync();

        var client = await _factory.CreateSignedInClientAsync();
        var response = await HtmlForm.PostAsync(
            client,
            $"/admin/orders/detail/{order.Id}",
            $"/admin/orders/changestatus/{order.Id}",
            new Dictionary<string, string> { ["next"] = ((int)OrderStatus.Onaylandi).ToString() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OrderStatus.Kargoda, (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.Status);
    }

    [Fact]
    public async Task Siparis_numarasiyla_arama_yalniz_eslesen_siparisi_getirir()
    {
        await using var context = TestDb.NewContext();
        var first = await PlaceOrderAsync(context);
        var second = await PlaceOrderAsync(context, "Cam Sürahi", "cam-surahi");
        var client = await _factory.CreateSignedInClientAsync();

        var html = await (await client.GetAsync($"/admin/orders?ara={second.OrderNo}")).Content.ReadAsStringAsync();

        Assert.Contains(second.OrderNo, html);
        Assert.DoesNotContain(first.OrderNo, html);
    }

    private static async Task<Order> PlaceOrderAsync(
        HerYerdeContext context,
        string name = "Çelik Tencere",
        string slug = "celik-tencere")
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, name, slug);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);

        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz",
            "05424970982",
            null,
            "Cumhuriyet Mah. 12/3",
            "İstanbul",
            "Kadıköy",
            null,
            PaymentMethod.KapidaOdeme));

        Assert.Equal(HttpStatusCode.Created, status);
        return result.Data!;
    }
}
