using System.Net;
using System.Text.RegularExpressions;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D13 B2: /admin panosu — bugünkü/haftalık sipariş ve ciro (rapor kuralı), bekleyen havale bildirimi, iade talebi, yorum,
/// düşük stok, kargoya verilmemiş gecikmiş Onaylandı sipariş, okunmamış mesaj. Sayılar elle hesapla eşit, sorgu sayısı sınırlı.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminDashboardTests : IAsyncLifetime
{
    private readonly CountingFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Saat 2026-01-15 Perşembe 09:30 UTC (İstanbul 12:30). Bugün: İstanbul 15 Ocak; hafta: Pazartesi 12 Ocak'tan.</summary>
    [Fact]
    public async Task Pano_sayilari_elle_hesapla_esit()
    {
        await using (var context = TestDb.NewContext())
        {
            await SeedAsync(context);
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var html = await (await admin.GetAsync("/admin")).Content.ReadAsStringAsync();

        // Bugün: A kapıda teslim 100 (ödendi), B havale onaylı 200 (ödendi), C havale bekleyen 300, G kart başarılı 700 (ödendi); F iptal sayılmaz.
        Assert.Equal("4", Metric(html, "today-orders"));
        Assert.Equal("1.000,00 ₺", Metric(html, "today-revenue"));
        // Hafta: bugünküler + D (Salı, kapıda onaylı 400, ödenmedi); E geçen hafta.
        Assert.Equal("5", Metric(html, "week-orders"));
        Assert.Equal("1.000,00 ₺", Metric(html, "week-revenue"));
        Assert.Equal("1", Metric(html, "pending-notices"));
        Assert.Equal("1", Metric(html, "pending-returns"));
        Assert.Equal("2", Metric(html, "pending-reviews"));
        Assert.Equal("1", Metric(html, "low-stock"));
        Assert.Equal("2", Metric(html, "unread-messages"));
        // E: 9 Ocak Cuma onaylı; iş günü 12-15 Ocak = 4 > 2. D: 13 Ocak Salı; 14-15 Ocak = 2, gecikmiş değil.
        Assert.Equal("1", Metric(html, "late-dispatch"));
        Assert.Contains("HY-TEST-E", html);
        Assert.DoesNotContain("HY-TEST-D\"", html);
    }

    [Fact]
    public async Task Pano_en_cok_sekiz_sorgu_atar()
    {
        await using (var context = TestDb.NewContext())
        {
            await SeedAsync(context);
        }

        var admin = await _factory.CreateSignedInClientAsync();

        Assert.InRange(await _factory.QueryCountAsync(admin, "/admin"), 1, 8);
    }

    private static string Metric(string html, string name)
    {
        var match = Regex.Match(html, $"data-metric=\"{name}\"[^>]*>\\s*([^<]+?)\\s*<");
        Assert.True(match.Success, $"{name} panoda yok.");
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task SeedAsync(HerYerdeContext context)
    {
        var now = TestClock.Now;
        Order NewOrder(string no, DateTime at, PaymentMethod method, OrderStatus status, decimal total) => new()
        {
            OrderNo = no,
            AccessToken = Guid.NewGuid(),
            Status = status,
            PaymentMethod = method,
            Subtotal = total,
            Total = total,
            FullName = "Pano Test",
            Phone = "05424970982",
            Address = "Adres",
            City = "İstanbul",
            District = "Kadıköy",
            CreatedAt = at
        };

        var a = NewOrder("HY-TEST-A", now.AddHours(-2), PaymentMethod.KapidaOdeme, OrderStatus.TeslimEdildi, 100m);
        var b = NewOrder("HY-TEST-B", now.AddHours(-3), PaymentMethod.HavaleEft, OrderStatus.Onaylandi, 200m);
        var c = NewOrder("HY-TEST-C", now.AddHours(-4), PaymentMethod.HavaleEft, OrderStatus.Beklemede, 300m);
        var d = NewOrder("HY-TEST-D", new DateTime(2026, 1, 13, 10, 0, 0, DateTimeKind.Utc), PaymentMethod.KapidaOdeme, OrderStatus.Onaylandi, 400m);
        var e = NewOrder("HY-TEST-E", new DateTime(2026, 1, 9, 10, 0, 0, DateTimeKind.Utc), PaymentMethod.KapidaOdeme, OrderStatus.Onaylandi, 500m);
        var f = NewOrder("HY-TEST-F", now.AddHours(-1), PaymentMethod.KapidaOdeme, OrderStatus.IptalEdildi, 600m);
        var g = NewOrder("HY-TEST-G", now.AddMinutes(-30), PaymentMethod.KrediKarti, OrderStatus.Beklemede, 700m);
        // İstanbul gününün başı (14 Ocak 21:00 UTC) öncesi: dün sayılır, bu haftadır.
        var yesterday = NewOrder("HY-TEST-Y", new DateTime(2026, 1, 14, 20, 0, 0, DateTimeKind.Utc), PaymentMethod.HavaleEft, OrderStatus.IptalEdildi, 50m);
        context.Orders.AddRange(a, b, c, d, e, f, g, yesterday);
        await context.SaveChangesAsync();

        context.Payments.Add(new Payment
        {
            OrderId = g.Id,
            Provider = "iyzico",
            ConversationId = Guid.NewGuid().ToString("n"),
            PaymentId = "pay-g",
            Status = PaymentStatus.Basarili,
            Amount = 700m,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.PaymentNotices.Add(new PaymentNotice { OrderId = c.Id, SenderName = "Pano", PaidOn = now.Date, Amount = 300m, CreatedAt = now });
        context.PaymentNotices.Add(new PaymentNotice { OrderId = b.Id, SenderName = "Pano", PaidOn = now.Date, Amount = 200m, CreatedAt = now, ApprovedAt = now });
        context.ReturnRequests.AddRange(
            new ReturnRequest { OrderId = a.Id, Type = ReturnType.Iade, Status = ReturnStatus.Bekliyor, Reason = "x", CreatedAt = now },
            new ReturnRequest { OrderId = a.Id, Type = ReturnType.Iade, Status = ReturnStatus.Reddedildi, Reason = "y", CreatedAt = now });
        var productId = await TestData.AddHomeProductAsync(context, "Az Kalan", "az-kalan", stock: 3);
        await TestData.AddHomeProductAsync(context, "Bol", "bol", stock: 50);
        await TestData.AddHomeProductAsync(context, "Takipsiz", "takipsiz");
        context.ProductReviews.AddRange(
            new ProductReview { ProductId = productId, Name = "a", Rating = 5, Comment = "a", CreatedAt = now },
            new ProductReview { ProductId = productId, Name = "b", Rating = 4, Comment = "b", CreatedAt = now },
            new ProductReview { ProductId = productId, Name = "c", Rating = 3, Comment = "c", CreatedAt = now, IsApproved = true });
        context.ContactMessages.AddRange(
            new ContactMessage { Name = "a", Contact = "a", Subject = ContactSubject.Diger, Message = "a", CreatedAt = now },
            new ContactMessage { Name = "b", Contact = "b", Subject = ContactSubject.Diger, Message = "b", CreatedAt = now },
            new ContactMessage { Name = "c", Contact = "c", Subject = ContactSubject.Diger, Message = "c", CreatedAt = now, ReadAt = now });
        await context.SaveChangesAsync();
    }
}
