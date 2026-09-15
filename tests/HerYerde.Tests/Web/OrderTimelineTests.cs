using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HerYerde.Tests.Web;

/// <summary>D13 B4: sipariş iç notları (denetim izli) ve tek akışta zaman çizelgesi: sipariş, postalar, durum değişimleri, notlar,
/// müşteri iptali eskiden yeniye.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderTimelineTests : IAsyncLifetime
{
    private readonly ClockFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Ic_not_kaydedilir_denetim_izine_yazilir_bos_not_400()
    {
        var order = await PlaceAsync();
        var admin = await _factory.CreateSignedInClientAsync();
        var detail = $"/admin/orders/detail/{order.Id}";

        var empty = await HtmlForm.PostAsync(admin, detail, $"/admin/orders/{order.Id}/not", new() { ["text"] = "  " });
        var added = await HtmlForm.PostAsync(admin, detail, $"/admin/orders/{order.Id}/not", new() { ["text"] = "Kapı zili bozuk, arayın." });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Found, added.StatusCode);
        await using var check = TestDb.NewContext();
        var note = await check.OrderNotes.AsNoTracking().SingleAsync();
        Assert.Equal(order.Id, note.OrderId);
        Assert.Equal("Kapı zili bozuk, arayın.", note.Text);
        Assert.Contains(await check.AdminAuditLogs.AsNoTracking().ToListAsync(), a => a.Action == "iç not" && a.EntityId == order.Id);
    }

    [Fact]
    public async Task Zaman_cizelgesi_siparis_posta_durum_not_ve_musteri_iptalini_eskiden_yeniye_siralar()
    {
        var order = await PlaceAsync();
        var admin = await _factory.CreateSignedInClientAsync();
        var detail = $"/admin/orders/detail/{order.Id}";

        _factory.Clock.Advance(TimeSpan.FromHours(1));
        await HtmlForm.PostAsync(admin, detail, $"/admin/orders/changestatus/{order.Id}", new() { ["next"] = ((int)OrderStatus.Onaylandi).ToString() });
        _factory.Clock.Advance(TimeSpan.FromHours(1));
        await HtmlForm.PostAsync(admin, detail, $"/admin/orders/{order.Id}/not", new() { ["text"] = "Müşteri akşam arasın." });
        _factory.Clock.Advance(TimeSpan.FromHours(1));
        var customer = _factory.CreateNonRedirectingClient();
        var cancelled = await HtmlForm.PostAsync(customer, "/siparis-sorgula", $"/siparis/{order.OrderNo}/iptal", new() { ["t"] = order.AccessToken.ToString() });
        Assert.Equal(HttpStatusCode.SeeOther, cancelled.StatusCode);

        var html = await (await admin.GetAsync(detail)).Content.ReadAsStringAsync();

        var items = Regex.Matches(html, "data-timeline-at=\"(?<at>[^\"]+)\" data-timeline-kind=\"(?<kind>[a-z]+)\"")
            .Select(m => (At: DateTime.Parse(m.Groups["at"].Value, null, System.Globalization.DateTimeStyles.RoundtripKind), Kind: m.Groups["kind"].Value))
            .ToList();
        Assert.Equal(["siparis", "posta", "posta", "islem", "not", "islem"], items.Select(i => i.Kind).ToList());
        Assert.Equal(items.Select(i => i.At).Order().ToList(), items.Select(i => i.At).ToList());
        Assert.Matches("Müşteri akşam arasın\\.</span>\\s*<span class=\"timeline__actor\">[^<#]+@[^<]+</span>", html);
        Assert.Contains("müşteri iptali", html);
    }

    [Fact]
    public async Task Anonimlestirme_ic_notun_metnini_gizler_satir_kalir()
    {
        var order = await PlaceAsync();
        var admin = await _factory.CreateSignedInClientAsync();
        var detail = $"/admin/orders/detail/{order.Id}";
        await HtmlForm.PostAsync(admin, detail, $"/admin/orders/{order.Id}/not", new() { ["text"] = "Ayşe Hanım 0542 497 09 82'den aradı." });
        await HtmlForm.PostAsync(admin, detail, $"/admin/orders/changestatus/{order.Id}", new() { ["next"] = ((int)OrderStatus.IptalEdildi).ToString() });

        var anonymized = await HtmlForm.PostAsync(admin, detail, $"/admin/orders/anonymize/{order.Id}", new());

        Assert.Equal(HttpStatusCode.Found, anonymized.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal("***", (await check.OrderNotes.AsNoTracking().SingleAsync()).Text);
    }

    private async Task<Order> PlaceAsync()
    {
        await using var context = TestDb.NewContext();
        var cartManager = TestData.NewCartManager(context, _factory.Clock);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);
        var (_, result) = await TestData.NewOrderManager(context, _factory.Clock).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", "05424970982", "ayse@example.com", "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, PaymentMethod.KapidaOdeme));
        return result.Data!;
    }

    /// <summary>İleri sarılabilir saatli fabrika: işlemler farklı anlarda yazılır.</summary>
    private sealed class ClockFactory : AdminWebFactory
    {
        public TestClock.MovableTimeProvider Clock { get; } = TestClock.Movable();

        protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton<TimeProvider>(Clock);
    }
}
