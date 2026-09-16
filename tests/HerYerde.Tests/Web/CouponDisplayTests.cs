using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>İYZİCO-DOĞRULAMA-2: kuponlu siparişte ara toplam + kargo, toplamı vermez. İndirim satırı
/// teşekkür sayfasında ve sipariş postasında da yazmazsa tutarlar tutarsız görünür.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CouponDisplayTests : IAsyncLifetime
{
    private const string Code = "HOSGELDIN";

    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Tesekkur_sayfasi_indirim_satirini_gosterir()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceWithCouponAsync(context);

        var html = await (await _factory.CreateNonRedirectingClient()
            .GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync();

        Assert.Contains("İndirim", html);
        Assert.Contains(Code, html);
        Assert.Contains("169,00", html);
        // Ara toplam + kargo - indirim = toplam; üçü de sayfada olmalı.
        Assert.Contains("1.690,00", html);
        Assert.Contains("1.600,90", html);
    }

    [Fact]
    public async Task Siparis_postasi_indirim_satirini_gosterir()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceWithCouponAsync(context);

        var mail = Assert.Single(await new EfOutboxMessageDal(context)
            .GetListAsync(m => m.Type == OutboxType.OrderPlaced && m.Subject.EndsWith(order.OrderNo)));

        Assert.Contains("İndirim", mail.Body);
        Assert.Contains(Code, mail.Body);
        Assert.Contains("169,00", mail.Body);
        Assert.Contains("1.600,90", mail.Body);
    }

    [Fact]
    public async Task Kuponsuz_siparisde_indirim_satiri_cizilmez()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceAsync(context, coupon: null);

        var html = await (await _factory.CreateNonRedirectingClient()
            .GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync();
        var mail = Assert.Single(await new EfOutboxMessageDal(context)
            .GetListAsync(m => m.Type == OutboxType.OrderPlaced && m.Subject.EndsWith(order.OrderNo)));

        Assert.DoesNotContain("İndirim", html);
        Assert.DoesNotContain("İndirim", mail.Body);
    }

    private static async Task<Order> PlaceWithCouponAsync(HerYerdeContext context)
    {
        await new EfCouponDal(context).AddAsync(new Coupon
        {
            Code = Code,
            Kind = CouponKind.Yuzde,
            Value = 10m,
            StartsAt = TestClock.Now.AddDays(-1),
            EndsAt = TestClock.Now.AddDays(7),
            IsActive = true,
            CreatedAt = TestClock.Now
        });
        await new EfUnitOfWork(context).SaveChangesAsync();
        return await PlaceAsync(context, Code);
    }

    private static async Task<Order> PlaceAsync(HerYerdeContext context, string? coupon)
    {
        var productId = await TestData.AddHomeProductAsync(context, "Kahvaltı Takımı", "kahvalti-takimi", price: 1690m, stock: 5);
        var cartManager = TestData.NewCartManager(context);
        var (_, cart) = await cartManager.GetOrCreateAsync(null);
        await cartManager.AddAsync(cart.Data!.Id, productId, null, 1);
        if (coupon is not null)
        {
            var (status, _) = await TestData.NewCouponManager(context).ApplyAsync(cart.Data.Id, coupon, 1690m);
            Assert.Equal(HttpStatusCode.OK, status);
        }

        var (placed, result) = await TestData.NewOrderManager(context).PlaceAsync(cart.Data.Id, new OrderDraft(
            "Ayşe Yılmaz",
            "0542 497 09 82",
            "ayse@ornek.test",
            "Cumhuriyet Mah. 12/3",
            "İstanbul",
            "Kadıköy",
            null,
            PaymentMethod.KapidaOdeme));
        Assert.Equal(HttpStatusCode.Created, placed);
        return result.Data!;
    }
}
