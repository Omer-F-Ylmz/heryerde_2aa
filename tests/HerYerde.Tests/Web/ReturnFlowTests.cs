using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D13 A1-A2 HTTP akışı: müşteri sipariş sayfasından iade talebi (fotoğraflı) açar ya da siparişi iptal eder; yönetici
/// /admin/iadeler'de onaylar, teslim alır, elle geri ödemeyi işaretler; her işlem denetim izinde.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReturnFlowTests : IAsyncLifetime
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private readonly PrivateRootFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Musteri_teslim_edilmis_sipariste_fotografli_iade_talebi_acar_yonetici_onaylar_teslim_alir_geri_odemeyi_isaretler()
    {
        await using var context = TestDb.NewContext();
        var (order, item) = await PlaceOrderAsync(context, PaymentMethod.KapidaOdeme, OrderStatus.TeslimEdildi);
        var client = _factory.CreateNonRedirectingClient();

        var thanks = await (await client.GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync();
        Assert.Contains($"href=\"/siparis/{order.OrderNo}/iade?t={order.AccessToken}\"", thanks);
        Assert.DoesNotContain($"action=\"/siparis/{order.OrderNo}/iptal\"", thanks);

        var formUrl = $"/siparis/{order.OrderNo}/iade?t={order.AccessToken}";
        var token = await HtmlForm.AntiforgeryTokenAsync(client, formUrl);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(order.AccessToken.ToString()), "t" },
            { new StringContent(((int)ReturnType.Iade).ToString()), "Type" },
            { new StringContent("Rengi fotoğraftakinden farklı."), "Reason" },
            { new StringContent("TR33 0006 1005 1978 6457 8413 26"), "Iban" },
            { new StringContent(item.Id.ToString()), "Lines[0].OrderItemId" },
            { new StringContent("1"), "Lines[0].Quantity" }
        };
        var photo = new ByteArrayContent(Png);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(photo, "Photo", "urun.png");

        var posted = await client.PostAsync($"/siparis/{order.OrderNo}/iade", content);

        Assert.Equal(HttpStatusCode.SeeOther, posted.StatusCode);
        var request = Assert.Single(await new EfReturnRequestDal(context).GetListAsync());
        Assert.Equal(ReturnStatus.Bekliyor, request.Status);
        Assert.Matches("^iadeler/[0-9a-f]{32}\\.png$", request.PhotoFile!);
        Assert.True(File.Exists(Path.Combine(_factory.PrivateRoot, request.PhotoFile!)));
        // İki adetin biri talep edildi: bağlantı durur; kalan da talep edilince bağlantı kalkar, form sipariş sayfasına döner.
        Assert.Contains($"href=\"{formUrl}\"", await (await client.GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync());
        await TestData.NewReturnManager(context, new FakePaymentProvider()).RequestAsync(order.OrderNo, order.AccessToken,
            new ReturnDraft(ReturnType.Iade, "İkincisi de.", [new ReturnLine(item.Id, 1)], "TR330006100519786457841326", null));
        Assert.DoesNotContain($"href=\"{formUrl}\"", await (await client.GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.SeeOther, (await client.GetAsync(formUrl)).StatusCode);

        var admin = await _factory.CreateSignedInClientAsync();
        var list = await (await admin.GetAsync("/admin/iadeler")).Content.ReadAsStringAsync();
        Assert.Contains(order.OrderNo, list);
        var detailUrl = $"/admin/iadeler/{request.Id}";
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{detailUrl}/foto")).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await HtmlForm.PostAsync(admin, detailUrl, $"{detailUrl}/onayla", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await HtmlForm.PostAsync(admin, detailUrl, $"{detailUrl}/teslim-al", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await HtmlForm.PostAsync(admin, detailUrl, $"{detailUrl}/geri-odendi", new())).StatusCode);

        await using var check = TestDb.NewContext();
        Assert.Equal(ReturnStatus.Tamamlandi, (await new EfReturnRequestDal(check).GetAsync(r => r.Id == request.Id))!.Status);
        var actions = (await new EfAdminAuditLogDal(check).GetListAsync(a => a.Entity == "iade" && a.EntityId == request.Id)).Select(a => a.Action).ToList();
        Assert.Equal(["iade onayı", "iade teslim alındı", "iade geri ödendi"], actions);
    }

    [Fact]
    public async Task Yonetici_ret_gerekcesiz_400_gerekceyle_reddeder()
    {
        await using var context = TestDb.NewContext();
        var (order, item) = await PlaceOrderAsync(context, PaymentMethod.KapidaOdeme, OrderStatus.TeslimEdildi);
        var (_, request) = await TestData.NewReturnManager(context, new FakePaymentProvider()).RequestAsync(order.OrderNo, order.AccessToken,
            new ReturnDraft(ReturnType.Iade, "Kırık geldi.", [new ReturnLine(item.Id, 1)], "TR330006100519786457841326", null));
        var admin = await _factory.CreateSignedInClientAsync();
        var detailUrl = $"/admin/iadeler/{request.Data!.Id}";

        var empty = await HtmlForm.PostAsync(admin, detailUrl, $"{detailUrl}/reddet", new() { ["reason"] = "" });
        var rejected = await HtmlForm.PostAsync(admin, detailUrl, $"{detailUrl}/reddet", new() { ["reason"] = "Kullanım izi var." });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Found, rejected.StatusCode);
        Assert.Contains(await new EfAdminAuditLogDal(context).GetListAsync(), a => a.Action == "iade reddi" && a.EntityId == request.Data.Id);
    }

    [Fact]
    public async Task Musteri_beklemedeki_siparisi_sayfasindan_iptal_eder_hazirlanan_sipariste_iptal_dugmesi_yok()
    {
        await using var context = TestDb.NewContext();
        var (pending, _) = await PlaceOrderAsync(context, PaymentMethod.KapidaOdeme, OrderStatus.Beklemede);
        var (preparing, _) = await PlaceOrderAsync(context, PaymentMethod.KapidaOdeme, OrderStatus.Hazirlaniyor, "hasir-sepet");
        var client = _factory.CreateNonRedirectingClient();
        var pendingUrl = $"/siparis/{pending.OrderNo}/tesekkur?t={pending.AccessToken}";

        var pendingHtml = await (await client.GetAsync(pendingUrl)).Content.ReadAsStringAsync();
        var preparingHtml = await (await client.GetAsync($"/siparis/{preparing.OrderNo}/tesekkur?t={preparing.AccessToken}")).Content.ReadAsStringAsync();
        var cancelled = await HtmlForm.PostAsync(client, pendingUrl, $"/siparis/{pending.OrderNo}/iptal", new() { ["t"] = pending.AccessToken.ToString() });
        var after = await (await client.GetAsync(pendingUrl)).Content.ReadAsStringAsync();
        var refused = await HtmlForm.PostAsync(client, "/siparis-sorgula", $"/siparis/{preparing.OrderNo}/iptal", new() { ["t"] = preparing.AccessToken.ToString() });

        Assert.Contains($"action=\"/siparis/{pending.OrderNo}/iptal\"", pendingHtml);
        Assert.DoesNotContain($"action=\"/siparis/{preparing.OrderNo}/iptal\"", preparingHtml);
        Assert.Equal(HttpStatusCode.SeeOther, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal(OrderStatus.IptalEdildi, (await new EfOrderDal(check).GetAsync(o => o.Id == pending.Id))!.Status);
        Assert.Equal(OrderStatus.Hazirlaniyor, (await new EfOrderDal(check).GetAsync(o => o.Id == preparing.Id))!.Status);
        Assert.Contains("Siparişiniz iptal edildi.", after);
    }

    [Fact]
    public async Task Iptal_edilen_onayli_havalenin_geri_odemesi_siparis_detayinda_isaretlenir()
    {
        await using var context = TestDb.NewContext();
        var (order, _) = await PlaceOrderAsync(context, PaymentMethod.HavaleEft, OrderStatus.Onaylandi);
        await TestData.NewReturnManager(context, new FakePaymentProvider()).CancelByCustomerAsync(
            order.OrderNo, order.AccessToken, "TR330006100519786457841326", "10.0.0.1");
        var admin = await _factory.CreateSignedInClientAsync();
        var detailUrl = $"/admin/orders/detail/{order.Id}";

        var html = await (await admin.GetAsync(detailUrl)).Content.ReadAsStringAsync();
        var marked = await HtmlForm.PostAsync(admin, detailUrl, $"/admin/orders/{order.Id}/geri-odendi", new());

        Assert.Contains("TR330006100519786457841326", html);
        Assert.Equal(HttpStatusCode.Found, marked.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.NotNull((await new EfOrderDal(check).GetAsync(o => o.Id == order.Id))!.RefundedAt);
        Assert.Contains(await new EfAdminAuditLogDal(check).GetListAsync(), a => a.Action == "havale geri ödendi" && a.EntityId == order.Id);
    }

    [Fact]
    public async Task Anonim_istek_admin_iadeler_sayfasinda_girise_yonlenir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/admin/iadeler");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/admin/auth/login", response.Headers.Location!.ToString());
    }

    /// <summary>Stok takipli ürünle sipariş açar, durumu doğrudan yazar (teslimde teslim anı sabit saat).</summary>
    private static async Task<(Order Order, OrderItem Item)> PlaceOrderAsync(
        HerYerdeContext context,
        PaymentMethod method,
        OrderStatus status,
        string slug = "celik-tencere")
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", slug, stock: 5);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 2);
        var (_, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", "05424970982", "ayse@example.com", "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, method));

        var tracked = (await new EfOrderDal(context).GetTrackedAsync(o => o.Id == result.Data!.Id))!;
        tracked.Status = status;
        tracked.DeliveredAt = status == OrderStatus.TeslimEdildi ? TestClock.Now : null;
        await new EfUnitOfWork(context).SaveChangesAsync();
        return (tracked, Assert.Single(await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == tracked.Id)));
    }
}
