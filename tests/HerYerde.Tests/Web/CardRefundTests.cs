using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D11 B6: kartla ödenmiş sipariş yönetimden tam tutar iade edilir: sağlayıcıda iptal/iade, Payment.Iade,
/// sipariş İptal edildi, stok geri. İkinci iade 409; kısmi iade yok.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CardRefundTests : IAsyncLifetime
{
    private readonly CardPaymentFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Iade_odemeyi_iade_yapar_siparisi_iptal_eder_stogu_geri_koyar()
    {
        var (orderId, productId) = await PaidCardOrderAsync();
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(admin, $"/admin/orders/detail/{orderId}", $"/admin/orders/{orderId}/iade", new());

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Equal(PaymentStatus.Iade, Assert.Single(await new EfPaymentDal(context).GetListAsync()).Status);
        Assert.Equal(OrderStatus.IptalEdildi, (await new EfOrderDal(context).GetAsync(o => o.Id == orderId))!.Status);
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
        var refund = Assert.Single(_factory.Provider.Refunds);
        Assert.Equal(529.90m, refund.Amount);
        Assert.Contains(await new EfAdminAuditLogDal(context).GetListAsync(), a => a.Action == "iade" && a.EntityId == orderId);
    }

    [Fact]
    public async Task Ikinci_iade_409_saglayici_bir_kez_cagrilir()
    {
        var (orderId, productId) = await PaidCardOrderAsync();
        var admin = await _factory.CreateSignedInClientAsync();
        await HtmlForm.PostAsync(admin, $"/admin/orders/detail/{orderId}", $"/admin/orders/{orderId}/iade", new());

        var second = await HtmlForm.PostAsync(admin, $"/admin/orders/detail/{orderId}", $"/admin/orders/{orderId}/iade", new());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Single(_factory.Provider.Refunds);
        await using var context = TestDb.NewContext();
        Assert.Equal(5, await TestData.ProductStockAsync(context, productId));
    }

    [Fact]
    public async Task Saglayici_iadeyi_reddederse_hicbir_sey_degismez()
    {
        var (orderId, productId) = await PaidCardOrderAsync();
        _factory.Provider.RefundFails = true;
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(admin, $"/admin/orders/detail/{orderId}", $"/admin/orders/{orderId}/iade", new());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Equal(PaymentStatus.Basarili, Assert.Single(await new EfPaymentDal(context).GetListAsync()).Status);
        Assert.Equal(4, await TestData.ProductStockAsync(context, productId));
    }

    private async Task<(int OrderId, int ProductId)> PaidCardOrderAsync()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        }

        var client = _factory.CreateNonRedirectingClient();
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });
        await HtmlForm.PostAsync(client, "/odeme", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Email"] = "ayse@example.com",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)PaymentMethod.KrediKarti).ToString(),
            ["CardHolderName"] = "AYSE YILMAZ",
            ["CardNumber"] = "5528790000000008",
            ["CardExpireMonth"] = "12",
            ["CardExpireYear"] = "2030",
            ["CardCvc"] = "123",
            ["LegalConsent"] = "true"
        });

        await using var check = TestDb.NewContext();
        var payment = Assert.Single(await new EfPaymentDal(check).GetListAsync());
        await _factory.CreateNonRedirectingClient().PostAsync("/odeme/3d-donus", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["status"] = "success",
            ["paymentId"] = payment.PaymentId ?? "pay-0",
            ["conversationId"] = payment.ConversationId,
            ["conversationData"] = "3ds-veri",
            ["mdStatus"] = "1",
            ["signature"] = "imza"
        }));
        Assert.Equal(4, await TestData.ProductStockAsync(check, productId));
        return (payment.OrderId, productId);
    }
}
