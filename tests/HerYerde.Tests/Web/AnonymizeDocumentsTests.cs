using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>KAPANIŞ-3 V-05/V-06/V-10: anonimleştirme D11 belgelerini de kapsar — dekont dosyası silinir, düzenleme izindeki
/// eski/yeni adres-telefon silinir, sipariş anahtarı yenilenir (eski bağlantıyla fatura inmez); fatura yasal belge olarak kalır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AnonymizeDocumentsTests : IAsyncLifetime
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");
    private readonly UploadFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Anonimlestirme_dekontu_siler_izdeki_kisisel_degeri_temizler_anahtari_yeniler_faturayi_korur()
    {
        var order = await PlaceTransferAsync();
        var admin = await _factory.CreateSignedInClientAsync();
        var anonymous = _factory.CreateNonRedirectingClient();

        await PostNoticeAsync(anonymous, order);
        await UploadInvoiceAsync(admin, order.Id);
        Assert.Equal(HttpStatusCode.Found, (await HtmlForm.PostAsync(admin, $"/admin/orders/{order.Id}/duzenle", $"/admin/orders/{order.Id}/duzenle",
            new Dictionary<string, string> { ["Address"] = "Bağdat Cad. 100/4", ["City"] = "İstanbul", ["District"] = "Kadıköy", ["Phone"] = "0532 111 22 33" })).StatusCode);

        string receipt;
        string invoice;
        await using (var context = TestDb.NewContext())
        {
            receipt = (await context.PaymentNotices.AsNoTracking().SingleAsync()).ReceiptFile!;
            invoice = (await context.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).InvoiceFile!;
            (await context.Orders.SingleAsync(o => o.Id == order.Id)).Status = OrderStatus.IptalEdildi;
            await context.SaveChangesAsync();
        }

        Assert.True(File.Exists(PrivatePath(receipt)));
        var response = await HtmlForm.PostAsync(admin, $"/admin/orders/detail/{order.Id}", $"/admin/orders/anonymize/{order.Id}", new());

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var check = TestDb.NewContext();
        var stored = await check.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.False(File.Exists(PrivatePath(receipt)));
        Assert.Null((await check.PaymentNotices.AsNoTracking().SingleAsync()).ReceiptFile);
        Assert.True(File.Exists(PrivatePath(invoice)));
        Assert.NotEqual(order.AccessToken, stored.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/siparis/{order.OrderNo}/fatura?t={order.AccessToken}")).StatusCode);
        var trail = await new EfAdminAuditLogDal(check).GetListAsync(a => a.EntityId == order.Id && a.Entity == "sipariş");
        Assert.Contains(trail, a => a.Action == "düzenle");
        Assert.DoesNotContain(trail, a => a.Detail != null && (a.Detail.Contains("Cumhuriyet") || a.Detail.Contains("0532")));
    }

    private string PrivatePath(string relative) => Path.Combine(_factory.Root, "private", relative);

    private static async Task<Order> PlaceTransferAsync()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 10);
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cart.AddAsync(cartId, productId, null, 1);
        return (await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", "0542 497 09 82", "ayse@example.com", "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, PaymentMethod.HavaleEft))).Item2.Data!;
    }

    private static async Task PostNoticeAsync(HttpClient client, Order order)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(client, $"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(order.AccessToken.ToString()), "t" },
            { new StringContent("Mehmet Yılmaz"), "SenderName" },
            { new StringContent("2026-01-15"), "PaidOn" },
            { new StringContent("529.90"), "Amount" }
        };
        var file = new ByteArrayContent(TestImage.Png(20, 20));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "Receipt", "dekont.png");
        Assert.Equal(HttpStatusCode.SeeOther, (await client.PostAsync($"/siparis/{order.OrderNo}/odeme-bildir", content)).StatusCode);
    }

    private static async Task UploadInvoiceAsync(HttpClient admin, int orderId)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(admin, $"/admin/orders/detail/{orderId}");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("HY2026000001"), "InvoiceNo" },
            { new StringContent("2026-01-15"), "InvoiceDate" }
        };
        var file = new ByteArrayContent(Pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "File", "fatura.pdf");
        Assert.Equal(HttpStatusCode.Found, (await admin.PostAsync($"/admin/orders/{orderId}/fatura", content)).StatusCode);
    }
}
