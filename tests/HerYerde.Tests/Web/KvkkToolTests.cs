using System.Net;
using System.Text.Json;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D13 B1: /admin/kvkk — telefon ya da e-postayla kişinin tüm kayıtları, JSON + PDF erişim dökümü, tümünü anonimleştirme
/// (dosyalar dahil), her işlem denetim izinde, başvuru kaydında 30 günlük yanıt sayacı.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class KvkkToolTests : IAsyncLifetime
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private readonly PrivateRootFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("0542 497 09 82")]
    [InlineData("905424970982")]
    [InlineData("+90 (542) 497-0982")]
    [InlineData("AYSE@example.com")]
    public async Task Arama_telefon_bicimlerinden_ve_epostadan_ayni_kisiyi_bulur(string query)
    {
        await using var context = TestDb.NewContext();
        await SeedPersonAsync(context);

        var (status, person) = await TestData.NewKvkkManager(context).FindAsync(query);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, person.Data!.Orders.Count);
        Assert.Single(person.Data.ContactMessages);
        Assert.DoesNotContain(person.Data.Orders, o => o.FullName == "Başka Kişi");
    }

    [Fact]
    public async Task Gecersiz_arama_400()
    {
        await using var context = TestDb.NewContext();

        var (status, _) = await TestData.NewKvkkManager(context).FindAsync("ayse");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Erisim_dokumu_tum_tablolari_icerir_json_ve_pdf()
    {
        await using var context = TestDb.NewContext();
        var seed = await SeedPersonAsync(context);
        var admin = await _factory.CreateSignedInClientAsync();

        var json = await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk/dokum-json", new() { ["ara"] = "05424970982" });
        var pdf = await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk/dokum-pdf", new() { ["ara"] = "05424970982" });

        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        Assert.Equal("application/json", json.Content.Headers.ContentType!.MediaType);
        using var document = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
        foreach (var table in new[] { "orders", "orderItems", "payments", "paymentNotices", "returnRequests", "returnRequestItems", "contactMessages", "reviews" })
        {
            Assert.True(document.RootElement.GetProperty(table).GetArrayLength() > 0, $"{table} boş");
        }

        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        var text = LegalD13Tests.PdfText(await pdf.Content.ReadAsByteArrayAsync());
        Assert.Contains(seed.Delivered.OrderNo, text);
        Assert.Contains("İade", text);
    }

    [Fact]
    public async Task Tumunu_anonimlestir_kapali_siparisi_maskeler_dekont_ve_iade_fotografini_siler_mesaji_siler_aciki_atlar()
    {
        await using var context = TestDb.NewContext();
        var seed = await SeedPersonAsync(context);
        var receipt = Path.Combine(_factory.PrivateRoot, seed.ReceiptFile);
        var photo = Path.Combine(_factory.PrivateRoot, seed.PhotoFile);
        Assert.True(File.Exists(receipt) && File.Exists(photo));
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk/anonimlestir", new() { ["ara"] = "ayse@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(seed.Open.OrderNo, await response.Content.ReadAsStringAsync());
        Assert.False(File.Exists(receipt));
        Assert.False(File.Exists(photo));
        await using var check = TestDb.NewContext();
        var delivered = await check.Orders.AsNoTracking().SingleAsync(o => o.Id == seed.Delivered.Id);
        Assert.Equal("A*** Y***", delivered.FullName);
        var request = await check.ReturnRequests.AsNoTracking().SingleAsync();
        Assert.Null(request.RefundIban);
        Assert.Null(request.PhotoFile);
        Assert.Equal("***", request.Reason);
        Assert.Equal("Ayşe Yılmaz", (await check.Orders.AsNoTracking().SingleAsync(o => o.Id == seed.Open.Id)).FullName);
        Assert.Empty(await check.ContactMessages.AsNoTracking().Where(m => m.Name == "Ayşe Yılmaz").ToListAsync());
        Assert.Equal("A*** Y***", (await check.ProductReviews.AsNoTracking().SingleAsync()).Name);
    }

    [Fact]
    public async Task Arama_dokum_ve_anonimlestirme_denetim_izine_kisi_maskeli_yazilir()
    {
        await using var context = TestDb.NewContext();
        await SeedPersonAsync(context);
        var admin = await _factory.CreateSignedInClientAsync();

        await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk", new() { ["ara"] = "05424970982" });
        await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk/dokum-json", new() { ["ara"] = "05424970982" });
        await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk/anonimlestir", new() { ["ara"] = "05424970982" });

        await using var check = TestDb.NewContext();
        var rows = await check.AdminAuditLogs.AsNoTracking().Where(a => a.Entity == "kvkk").OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(["kvkk arama", "kvkk erişim dökümü", "kvkk anonimleştirme"], rows.Select(r => r.Action).ToList());
        Assert.All(rows, r => Assert.DoesNotContain("05424970982", r.Detail ?? string.Empty));
        Assert.All(rows, r => Assert.Contains("05*******82", r.Detail));
    }

    [Fact]
    public async Task Basvuru_kaydi_30_gun_yanit_sayaci_tutar_tamamlaninca_kapanir()
    {
        await using var context = TestDb.NewContext();
        var clock = TestClock.Movable();
        var kvkk = TestData.NewKvkkManager(context, clock);

        var (created, request) = await kvkk.OpenRequestAsync("0542 497 09 82", TestClock.Now);
        clock.Advance(TimeSpan.FromDays(25));
        var afterFive = Assert.Single((await kvkk.GetRequestsAsync()).Item2.Data!);
        clock.Advance(TimeSpan.FromDays(6));
        var overdue = Assert.Single((await kvkk.GetRequestsAsync()).Item2.Data!);
        await kvkk.CompleteRequestAsync(request.Data!.Id);
        var done = Assert.Single((await kvkk.GetRequestsAsync()).Item2.Data!);

        Assert.Equal(HttpStatusCode.Created, created);
        Assert.Equal("05424970982", request.Data.Subject);
        Assert.Equal(5, afterFive.DaysLeft);
        Assert.Equal(-1, overdue.DaysLeft);
        Assert.Null(done.DaysLeft);
        Assert.Equal(clock.Moment, done.Request.CompletedAt);
    }

    [Fact]
    public async Task Anonim_istek_admin_kvkk_sayfasinda_girise_yonlenir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/admin/kvkk");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/admin/auth/login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Aydinlatma_envanter_ve_lisans_notu_kvkk_basvurusunu_ic_notu_ve_barkodu_kapsar()
    {
        var kvkk = await (await _factory.CreateNonRedirectingClient().GetAsync("/yasal/kvkk-aydinlatma")).Content.ReadAsStringAsync();
        var inventory = RepoFile.ReadAllText("docs", "veri-envanteri.md");
        var licenses = RepoFile.ReadAllText("docs", "lisans-notlari.md");

        Assert.Contains("KVKK başvuru kaydı", kvkk);
        Assert.Contains("`kvkk_request`", inventory);
        Assert.Contains("`order_note`", inventory);
        Assert.Contains("Erişim dökümü", inventory);
        Assert.Contains("ZXing.Net 0.16.11", licenses);
    }

    private sealed record Seed(Order Delivered, Order Open, string ReceiptFile, string PhotoFile);

    /// <summary>Aynı kişinin iki havale siparişi (biri teslim edilmiş: bildirim + dekont, iade talebi + fotoğraf, yorum), iletişim
    /// mesajı; başka bir kişinin siparişi ve mesajı.</summary>
    private async Task<Seed> SeedPersonAsync(HerYerdeContext context)
    {
        var delivered = await PlaceAsync(context, "Ayşe Yılmaz", "05424970982", "ayse@example.com", "celik-tencere");
        var open = await PlaceAsync(context, "Ayşe Yılmaz", "0542 497 09 82", "ayse@example.com", "hasir-sepet");
        await PlaceAsync(context, "Başka Kişi", "05321112233", "baska@example.com", "bambu-kase");

        Directory.CreateDirectory(Path.Combine(_factory.PrivateRoot, "dekontlar"));
        Directory.CreateDirectory(Path.Combine(_factory.PrivateRoot, "iadeler"));
        var receipt = $"dekontlar/{Guid.NewGuid():n}.png";
        var photo = $"iadeler/{Guid.NewGuid():n}.png";
        await File.WriteAllBytesAsync(Path.Combine(_factory.PrivateRoot, receipt), Png);
        await File.WriteAllBytesAsync(Path.Combine(_factory.PrivateRoot, photo), Png);

        context.Payments.Add(new Payment
        {
            OrderId = open.Id,
            Provider = "iyzico",
            ConversationId = Guid.NewGuid().ToString("n"),
            Status = PaymentStatus.Basarisiz,
            Amount = open.Total,
            CreatedAt = TestClock.Now,
            UpdatedAt = TestClock.Now
        });
        await TestData.NewOrderManager(context).SubmitPaymentNoticeAsync(delivered.OrderNo, delivered.AccessToken,
            new PaymentNoticeDraft("Ayşe Yılmaz", TestClock.Now, delivered.Total, receipt));
        var tracked = (await new EfOrderDal(context).GetTrackedAsync(o => o.Id == delivered.Id))!;
        tracked.Status = OrderStatus.TeslimEdildi;
        tracked.DeliveredAt = TestClock.Now;
        await new EfUnitOfWork(context).SaveChangesAsync();
        var item = Assert.Single(await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == delivered.Id));
        var (requested, _) = await TestData.NewReturnManager(context, new FakePaymentProvider()).RequestAsync(delivered.OrderNo, delivered.AccessToken,
            new ReturnDraft(ReturnType.Iade, "Beğenmedim, Ayşe.", [new ReturnLine(item.Id, 1)], "TR330006100519786457841326", photo));
        Assert.Equal(HttpStatusCode.Created, requested);

        context.ProductReviews.Add(new ProductReview
        {
            ProductId = (await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere"))!.Id,
            Name = "Ayşe Yılmaz",
            Rating = 5,
            Comment = "Güzel.",
            IsApproved = true,
            CreatedAt = TestClock.Now,
            OrderNo = delivered.OrderNo
        });
        context.ContactMessages.AddRange(
            new ContactMessage { Name = "Ayşe Yılmaz", Contact = "0 (542) 497-0982", Subject = ContactSubject.Iade, Message = "İade?", CreatedAt = TestClock.Now },
            new ContactMessage { Name = "Başka Kişi", Contact = "baska@example.com", Subject = ContactSubject.Diger, Message = "Merhaba", CreatedAt = TestClock.Now });
        await context.SaveChangesAsync();
        return new Seed(delivered, open, receipt, photo);
    }

    private static async Task<Order> PlaceAsync(HerYerdeContext context, string name, string phone, string email, string slug)
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Ürün " + slug, slug, stock: 5);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);
        var (_, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            name, phone, email, "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, PaymentMethod.HavaleEft));
        return result.Data!;
    }
}
