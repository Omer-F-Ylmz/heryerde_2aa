using System.Net;
using HerYerde.Business.Concrete;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D14 B4: indirim kuponu. Kod sepete yazılır, sipariş anında yeniden doğrulanır; kullanım
/// limitleri iptal edilmemiş siparişlerden sayılır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CouponTests : IAsyncLifetime
{
    private const string Code = "HOSGELDIN";

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Yuzde_kuponu_ara_toplamdan_dusulur_ve_sepette_gorunur()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.Yuzde, 10m);
        var cartId = await CartAsync(context, price: 1000m, quantity: 2);

        var (status, _) = await TestData.NewCouponManager(context).ApplyAsync(cartId, "hosgeldin", 2000m);
        var (_, view) = await TestData.NewCartManager(context).GetAsync(cartId);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Code, view.Data!.CouponCode);
        Assert.Equal(200m, view.Data.Discount);
        Assert.Equal(2000m, view.Data.Subtotal);
        // 2000 ara toplam bedava kargo eşiğinin altında: 2000 + 79,90 - 200.
        Assert.Equal(2000m + TestData.ShippingFee - 200m, view.Data.Total);
    }

    [Fact]
    public async Task Min_sepet_altinda_kupon_reddedilir()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.Yuzde, 10m, minSubtotal: 500m);
        var cartId = await CartAsync(context, price: 100m, quantity: 1);

        var (status, result) = await TestData.NewCouponManager(context).ApplyAsync(cartId, Code, 100m);
        var (_, view) = await TestData.NewCartManager(context).GetAsync(cartId);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("500", result.Message);
        Assert.Null(view.Data!.CouponCode);
        Assert.Equal(0m, view.Data.Discount);
    }

    [Fact]
    public async Task Suresi_dolmus_kupon_reddedilir()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.Tutar, 50m, endsAt: TestClock.Now.AddDays(-1));
        var cartId = await CartAsync(context, price: 1000m, quantity: 1);

        var (status, result) = await TestData.NewCouponManager(context).ApplyAsync(cartId, Code, 1000m);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("süresi", result.Message);
    }

    [Fact]
    public async Task Kapali_kupon_ve_bilinmeyen_kod_reddedilir()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.Tutar, 50m, active: false);
        var cartId = await CartAsync(context, price: 1000m, quantity: 1);
        var manager = TestData.NewCouponManager(context);

        var (closed, _) = await manager.ApplyAsync(cartId, Code, 1000m);
        var (unknown, _) = await manager.ApplyAsync(cartId, "YOKBOYLE", 1000m);

        Assert.Equal(HttpStatusCode.BadRequest, closed);
        Assert.Equal(HttpStatusCode.BadRequest, unknown);
    }

    [Fact]
    public async Task Toplam_kullanim_limiti_dolunca_reddedilir()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.Tutar, 50m, totalLimit: 1);
        await PlaceWithCouponAsync(context, "0542 497 09 82", "ayse@ornek.test");

        var cartId = await CartAsync(context, price: 1000m, quantity: 1);
        var (status, result) = await TestData.NewCouponManager(context).ApplyAsync(cartId, Code, 1000m);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("kullanım", result.Message);
    }

    [Fact]
    public async Task Kisi_basi_limit_asilinca_siparis_reddedilir()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.Tutar, 50m, perPersonLimit: 1);
        await PlaceWithCouponAsync(context, "0542 497 09 82", "ayse@ornek.test");

        // Aynı telefon ikinci kez: sepete yazılabilir ama sipariş açılamaz.
        var cartId = await CartAsync(context, price: 1000m, quantity: 1);
        Assert.Equal(HttpStatusCode.OK, (await TestData.NewCouponManager(context).ApplyAsync(cartId, Code, 1000m)).Item1);

        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft("0542 497 09 82", "baska@ornek.test"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("daha önce", result.Message);
        Assert.Single(await new EfOrderDal(context).GetListAsync());
    }

    [Fact]
    public async Task Kargo_bedava_kuponu_kargoyu_sifirlar()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.KargoBedava, 0m);
        var cartId = await CartAsync(context, price: 1000m, quantity: 1);
        await TestData.NewCouponManager(context).ApplyAsync(cartId, Code, 1000m);

        var (_, view) = await TestData.NewCartManager(context).GetAsync(cartId);
        var (status, placed) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft("0542 497 09 82", "ayse@ornek.test"));

        Assert.Equal(0m, view.Data!.ShippingFee);
        Assert.Equal(1000m, view.Data.Total);
        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal(0m, placed.Data!.ShippingFee);
        Assert.Equal(0m, placed.Data.Discount);
        Assert.Equal(1000m, placed.Data.Total);
        Assert.Equal(Code, placed.Data.CouponCode);
    }

    [Fact]
    public async Task Siparis_kupon_kodunu_ve_indirimi_saklar()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.Tutar, 150m);
        var cartId = await CartAsync(context, price: 1000m, quantity: 1);
        await TestData.NewCouponManager(context).ApplyAsync(cartId, Code, 1000m);

        var (status, placed) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft("0542 497 09 82", "ayse@ornek.test"));

        Assert.Equal(HttpStatusCode.Created, status);
        var order = (await new EfOrderDal(context).GetAsync(o => o.Id == placed.Data!.Id))!;
        Assert.Equal(Code, order.CouponCode);
        Assert.Equal(150m, order.Discount);
        Assert.Equal(1000m, order.Subtotal);
        Assert.Equal(1000m + TestData.ShippingFee - 150m, order.Total);
    }

    [Fact]
    public async Task Rapor_indirim_toplamini_verir()
    {
        await using var context = TestDb.NewContext();
        await AddCouponAsync(context, CouponKind.Tutar, 150m);
        await PlaceWithCouponAsync(context, "0542 497 09 82", "ayse@ornek.test");
        await PlaceWithCouponAsync(context, "0532 111 22 33", "veli@ornek.test");

        var report = await new ReportManager(
            new EfOrderDal(context),
            new EfOrderItemDal(context),
            new EfPaymentDal(context)).BuildAsync(
            TestClock.Now.AddDays(-1),
            TestClock.Now.AddDays(1),
            ReportPeriod.Gun,
            TimeZoneInfo.Utc);

        Assert.Equal(300m, report.Discount);
    }

    private static OrderDraft Draft(string phone, string? email) => new(
        "Ayşe Yılmaz",
        phone,
        email,
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        null,
        PaymentMethod.KapidaOdeme);

    private static async Task PlaceWithCouponAsync(HerYerdeContext context, string phone, string? email)
    {
        var cartId = await CartAsync(context, price: 1000m, quantity: 1);
        await TestData.NewCouponManager(context).ApplyAsync(cartId, Code, 1000m);
        var (status, _) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft(phone, email));
        Assert.Equal(HttpStatusCode.Created, status);
    }

    private static async Task<Guid> CartAsync(HerYerdeContext context, decimal price, int quantity)
    {
        var slug = "urun-" + Guid.NewGuid().ToString("n")[..8];
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", slug, price: price, stock: 50);
        var cartManager = TestData.NewCartManager(context);
        var (_, cart) = await cartManager.GetOrCreateAsync(null);
        await cartManager.AddAsync(cart.Data!.Id, productId, null, quantity);
        return cart.Data.Id;
    }

    private static async Task AddCouponAsync(
        HerYerdeContext context,
        CouponKind kind,
        decimal value,
        decimal minSubtotal = 0m,
        DateTime? endsAt = null,
        int? totalLimit = null,
        int? perPersonLimit = null,
        bool active = true)
    {
        await new EfCouponDal(context).AddAsync(new Coupon
        {
            Code = Code,
            Kind = kind,
            Value = value,
            MinSubtotal = minSubtotal,
            StartsAt = TestClock.Now.AddDays(-7),
            EndsAt = endsAt ?? TestClock.Now.AddDays(7),
            TotalLimit = totalLimit,
            PerPersonLimit = perPersonLimit,
            IsActive = active,
            CreatedAt = TestClock.Now
        });
        await new EfUnitOfWork(context).SaveChangesAsync();
    }
}
