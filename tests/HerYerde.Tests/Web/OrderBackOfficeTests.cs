using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D11 B1/B3/B4/B5: manuel sipariş formu, fatura (PDF, token'lı indirme, faturasız süzgeç), havale bildirimi
/// ve onayı, sipariş düzenleme (denetim izi, stok farkı, Kargoda sonrası 409).</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderBackOfficeTests : IAsyncLifetime
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
    public async Task Manuel_siparis_formu_kaynak_ve_kalemle_siparis_acar()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var response = await HtmlForm.PostAsync(admin, "/admin/orders/new", "/admin/orders/new", new Dictionary<string, string>
        {
            ["FullName"] = "Fatma Demir",
            ["Phone"] = "0532 111 22 33",
            ["Address"] = "Atatürk Cad. 5",
            ["City"] = "Ankara",
            ["District"] = "Çankaya",
            ["PaymentMethod"] = ((int)PaymentMethod.KapidaOdeme).ToString(),
            ["Source"] = ((int)OrderSource.Instagram).ToString(),
            ["Lines[0].Code"] = "celik-tencere",
            ["Lines[0].Quantity"] = "2",
            ["Lines[1].Code"] = "",
            ["Lines[1].Quantity"] = "1",
            ["ConsentConfirmed"] = "true"
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var check = TestDb.NewContext();
        var order = Assert.Single(await new EfOrderDal(check).GetListAsync());
        Assert.Equal(OrderSource.Instagram, order.Source);
        Assert.Equal($"/admin/orders/detail/{order.Id}", response.Headers.Location!.OriginalString);
        Assert.Contains(await new EfAdminAuditLogDal(check).GetListAsync(), a => a.Action == "manuel sipariş" && a.EntityId == order.Id);
    }

    [Fact]
    public async Task Fatura_pdf_yuklenir_calistirilabilir_icerik_400()
    {
        var order = await PlaceAsync(PaymentMethod.KapidaOdeme);
        var admin = await _factory.CreateSignedInClientAsync();

        var exe = await UploadInvoiceAsync(admin, order.Id, "fatura.pdf", [0x4D, 0x5A, 0x90, 0x00]);
        var pdf = await UploadInvoiceAsync(admin, order.Id, "fatura.pdf", Pdf);

        Assert.Equal(HttpStatusCode.BadRequest, exe.StatusCode);
        Assert.Equal(HttpStatusCode.Found, pdf.StatusCode);
        await using var context = TestDb.NewContext();
        var stored = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal("HY2026000001", stored.InvoiceNo);
        Assert.Equal(new DateTime(2026, 1, 15), stored.InvoiceDate);
        Assert.NotNull(stored.InvoiceFile);
        Assert.DoesNotContain("wwwroot", stored.InvoiceFile);
    }

    [Fact]
    public async Task Fatura_indirme_yalniz_siparisin_tokeniyla_yabanci_token_404()
    {
        var order = await PlaceAsync(PaymentMethod.KapidaOdeme);
        var other = await PlaceAsync(PaymentMethod.KapidaOdeme, slug: "cam-surahi");
        var admin = await _factory.CreateSignedInClientAsync();
        await UploadInvoiceAsync(admin, order.Id, "fatura.pdf", Pdf);
        var client = _factory.CreateNonRedirectingClient();

        var own = await client.GetAsync($"/siparis/{order.OrderNo}/fatura?t={order.AccessToken}");
        var foreign = await client.GetAsync($"/siparis/{order.OrderNo}/fatura?t={other.AccessToken}");
        var page = await (await client.GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("application/pdf", own.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Pdf, await own.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Contains("Faturayı indir", page);
    }

    [Fact]
    public async Task Faturasiz_teslim_edilenler_suzgeci()
    {
        var withInvoice = await PlaceAsync(PaymentMethod.KapidaOdeme);
        var without = await PlaceAsync(PaymentMethod.KapidaOdeme, slug: "cam-surahi");
        var open = await PlaceAsync(PaymentMethod.KapidaOdeme, slug: "hasir-sepet");
        await using (var context = TestDb.NewContext())
        {
            foreach (var id in new[] { withInvoice.Id, without.Id })
            {
                var order = await context.Orders.SingleAsync(o => o.Id == id);
                order.Status = OrderStatus.TeslimEdildi;
            }

            (await context.Orders.SingleAsync(o => o.Id == withInvoice.Id)).InvoiceNo = "HY2026000009";
            await context.SaveChangesAsync();
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var html = await (await admin.GetAsync("/admin/orders?faturasiz=1")).Content.ReadAsStringAsync();

        Assert.Contains(without.OrderNo, html);
        Assert.DoesNotContain(withInvoice.OrderNo, html);
        Assert.DoesNotContain(open.OrderNo, html);
    }

    [Fact]
    public async Task Havale_bildirimi_kaydedilir_onaylaninca_siparis_onaylanir_musteriye_posta()
    {
        var order = await PlaceAsync(PaymentMethod.HavaleEft);
        var client = _factory.CreateNonRedirectingClient();

        var notice = await PostNoticeAsync(client, order);
        Assert.Equal(HttpStatusCode.SeeOther, notice.StatusCode);

        PaymentNotice saved;
        await using (var context = TestDb.NewContext())
        {
            saved = Assert.Single(await context.PaymentNotices.AsNoTracking().ToListAsync());
            Assert.Equal(order.Id, saved.OrderId);
            Assert.Equal(529.90m, saved.Amount);
            Assert.NotNull(saved.ReceiptFile);
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var detail = await (await admin.GetAsync($"/admin/orders/detail/{order.Id}")).Content.ReadAsStringAsync();
        Assert.Contains("Ayşe Yılmaz", detail);
        var approved = await HtmlForm.PostAsync(admin, $"/admin/orders/detail/{order.Id}", $"/admin/orders/{order.Id}/havale-onayla", new());

        Assert.Equal(HttpStatusCode.Found, approved.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal(OrderStatus.Onaylandi, (await new EfOrderDal(check).GetAsync(o => o.Id == order.Id))!.Status);
        Assert.NotNull((await check.PaymentNotices.AsNoTracking().SingleAsync()).ApprovedAt);
        Assert.Contains(await new EfOutboxMessageDal(check).GetListAsync(), m => m.Type == OutboxType.PaymentApproved && m.To == "ayse@example.com");
    }

    [Fact]
    public async Task Havale_bildiriminde_altinci_istek_429()
    {
        var order = await PlaceAsync(PaymentMethod.HavaleEft);
        var client = _factory.CreateNonRedirectingClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await PostNoticeAsync(client, order, receipt: false)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostNoticeAsync(client, order, receipt: false)).StatusCode);
    }

    [Fact]
    public async Task Adres_duzenlemesi_denetim_izine_eskiden_yeniye_yazilir()
    {
        var order = await PlaceAsync(PaymentMethod.KapidaOdeme);
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await PostEditAsync(admin, order, address: "Bağdat Cad. 100/4", quantity: 1);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Equal("Bağdat Cad. 100/4", (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.Address);
        var audit = Assert.Single(await new EfAdminAuditLogDal(context).GetListAsync(a => a.Action == "düzenle" && a.EntityId == order.Id));
        Assert.Contains("adres: Cumhuriyet Mah. 12/3 → Bağdat Cad. 100/4", audit.Detail);
    }

    [Fact]
    public async Task Kalem_adedi_dusunce_stok_geri_doner_toplam_yenilenir()
    {
        var order = await PlaceAsync(PaymentMethod.KapidaOdeme, quantity: 3);
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await PostEditAsync(admin, order, address: "Cumhuriyet Mah. 12/3", quantity: 1);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var context = TestDb.NewContext();
        var product = (await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere"))!;
        Assert.Equal(9, product.Stock);
        var stored = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal(450m, stored.Subtotal);
        Assert.Equal(450m + stored.ShippingFee, stored.Total);
        Assert.Equal(1, Assert.Single(await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == order.Id)).Quantity);
    }

    [Fact]
    public async Task Onayli_havale_siparisinde_adet_degismez_409_toplam_ve_stok_ayni()
    {
        var order = await PlaceAsync(PaymentMethod.HavaleEft, quantity: 3);
        await using (var context = TestDb.NewContext())
        {
            (await context.Orders.SingleAsync(o => o.Id == order.Id)).Status = OrderStatus.Onaylandi;
            await context.SaveChangesAsync();
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var response = await PostEditAsync(admin, order, address: "Cumhuriyet Mah. 12/3", quantity: 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal(order.Total, (await new EfOrderDal(check).GetAsync(o => o.Id == order.Id))!.Total);
        Assert.Equal(3, Assert.Single(await new EfOrderItemDal(check).GetListAsync(i => i.OrderId == order.Id)).Quantity);
        Assert.Equal(7, (await new EfProductDal(check).GetAsync(p => p.Slug == "celik-tencere"))!.Stock);
    }

    [Fact]
    public async Task Kargodaki_siparis_duzenlenemez_409()
    {
        var order = await PlaceAsync(PaymentMethod.KapidaOdeme);
        await using (var context = TestDb.NewContext())
        {
            (await context.Orders.SingleAsync(o => o.Id == order.Id)).Status = OrderStatus.Kargoda;
            await context.SaveChangesAsync();
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var token = await HtmlForm.AntiforgeryTokenAsync(admin, $"/admin/orders/detail/{order.Id}");
        var response = await admin.PostAsync($"/admin/orders/{order.Id}/duzenle", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Address"] = "Yeni adres 1",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["Phone"] = "0542 497 09 82",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal("Cumhuriyet Mah. 12/3", (await new EfOrderDal(check).GetAsync(o => o.Id == order.Id))!.Address);
    }

    private static async Task<HttpResponseMessage> PostEditAsync(HttpClient admin, Order order, string address, int quantity)
    {
        await using var context = TestDb.NewContext();
        var item = Assert.Single(await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == order.Id));
        return await HtmlForm.PostAsync(admin, $"/admin/orders/{order.Id}/duzenle", $"/admin/orders/{order.Id}/duzenle", new Dictionary<string, string>
        {
            ["Address"] = address,
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["Phone"] = "0542 497 09 82",
            ["Note"] = "",
            ["Items[0].Id"] = item.Id.ToString(),
            ["Items[0].Quantity"] = quantity.ToString()
        });
    }

    private async Task<HttpResponseMessage> UploadInvoiceAsync(HttpClient admin, int orderId, string fileName, byte[] bytes)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(admin, $"/admin/orders/detail/{orderId}");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("HY2026000001"), "InvoiceNo" },
            { new StringContent("2026-01-15"), "InvoiceDate" }
        };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "File", fileName);
        return await admin.PostAsync($"/admin/orders/{orderId}/fatura", content);
    }

    private static async Task<HttpResponseMessage> PostNoticeAsync(HttpClient client, Order order, bool receipt = true)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(client, $"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(order.AccessToken.ToString()), "t" },
            { new StringContent("Ayşe Yılmaz"), "SenderName" },
            { new StringContent("2026-01-15"), "PaidOn" },
            { new StringContent("529.90"), "Amount" }
        };
        if (receipt)
        {
            var file = new ByteArrayContent(TestImage.Png(40, 60));
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(file, "Receipt", "dekont.png");
        }

        return await client.PostAsync($"/siparis/{order.OrderNo}/odeme-bildir", content);
    }

    private static async Task<Order> PlaceAsync(PaymentMethod method, string slug = "celik-tencere", int quantity = 1)
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, slug, slug, stock: 10);
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cart.AddAsync(cartId, productId, null, quantity);
        return (await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", "0542 497 09 82", "ayse@example.com", "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, method))).Item2.Data!;
    }
}
