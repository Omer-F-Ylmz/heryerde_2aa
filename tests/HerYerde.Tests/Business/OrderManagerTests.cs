using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class OrderManagerTests : IAsyncLifetime
{
    private static readonly OrderDraft Draft = new(
        "Ayşe Yılmaz",
        "0542 497 09 82",
        "ayse@example.com",
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        "Kapıya gelmeden arayın.",
        PaymentMethod.KapidaOdeme);

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Siparis_numarasi_gun_icinde_sirayla_artar()
    {
        await using var context = TestDb.NewContext();
        var prefix = OrderNo.PrefixFor(DateTime.UtcNow);

        var first = await PlaceAsync(context, quantity: 1);
        var second = await PlaceAsync(context, quantity: 1, name: "Cam Sürahi", slug: "cam-surahi");

        Assert.Equal(prefix + "0001", first.OrderNo);
        Assert.Equal(prefix + "0002", second.OrderNo);
    }

    [Fact]
    public async Task Siparis_toplami_ara_toplam_arti_kargodur()
    {
        await using var context = TestDb.NewContext();

        var order = await PlaceAsync(context, quantity: 2, price: 450m);

        Assert.Equal(900m, order.Subtotal);
        Assert.Equal(TestData.ShippingFee, order.ShippingFee);
        Assert.Equal(900m + TestData.ShippingFee, order.Total);
    }

    [Fact]
    public async Task Gecersiz_telefonla_siparis_olusmaz()
    {
        await using var context = TestDb.NewContext();
        var (cartId, _) = await FilledCartAsync(context, quantity: 1);

        var (status, result) = await TestData.NewOrderManager(context)
            .PlaceAsync(cartId, Draft with { Phone = "0212 497 09 82" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("telefon", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await new EfOrderDal(context).GetListAsync());
    }

    [Fact]
    public async Task Telefon_tek_bicimde_kaydedilir()
    {
        await using var context = TestDb.NewContext();

        var order = await PlaceAsync(context, quantity: 1);

        Assert.Equal("05424970982", order.Phone);
    }

    [Fact]
    public async Task Varyantli_urunde_siparis_stogu_dusurur()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var cartManager = TestData.NewCartManager(context);
        var (productId, variantId) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 5);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId, quantity: 2);

        var (status, _) = await manager.PlaceAsync(cartId, Draft);

        Assert.Equal(HttpStatusCode.Created, status);
        var variant = await new EfProductVariantDal(context).GetAsync(v => v.Id == variantId);
        Assert.Equal(3, variant!.Stock);
    }

    [Fact]
    public async Task Stok_yetersizse_409_doner_ve_stok_degismez()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var cartManager = TestData.NewCartManager(context);
        var (productId, variantId) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 5);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId, quantity: 2);

        var variant = (await new EfProductVariantDal(context).GetTrackedAsync(v => v.Id == variantId))!;
        variant.Stock = 1;
        await new EfUnitOfWork(context).SaveChangesAsync();

        var (status, result) = await manager.PlaceAsync(cartId, Draft);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("Şalvar", result.Message);
        Assert.Empty(await new EfOrderDal(context).GetListAsync());
        Assert.Equal(1, (await new EfProductVariantDal(context).GetAsync(v => v.Id == variantId))!.Stock);
    }

    [Fact]
    public async Task Stok_yetersiz_satir_sepette_isaretlenir()
    {
        await using var context = TestDb.NewContext();
        var cartManager = TestData.NewCartManager(context);
        var (productId, variantId) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 5);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId, quantity: 4);

        var variant = (await new EfProductVariantDal(context).GetTrackedAsync(v => v.Id == variantId))!;
        variant.Stock = 1;
        await new EfUnitOfWork(context).SaveChangesAsync();

        var line = (await cartManager.GetAsync(cartId)).Item2.Data!.Lines[0];
        Assert.True(line.Insufficient);
    }

    [Fact]
    public async Task Ev_urununde_stok_dusumu_yapilmaz()
    {
        await using var context = TestDb.NewContext();

        var order = await PlaceAsync(context, quantity: 3);

        Assert.Equal(OrderStatus.Beklemede, order.Status);
        Assert.Empty(await new EfProductVariantDal(context).GetListAsync());
    }

    [Fact]
    public async Task Siparis_satirlari_ad_ve_fiyat_kopyasini_tutar()
    {
        await using var context = TestDb.NewContext();

        var order = await PlaceAsync(context, quantity: 2, price: 450m);

        var item = Assert.Single(await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == order.Id));
        Assert.Equal("Çelik Tencere", item.ProductName);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(450m, item.UnitPrice);
    }

    [Fact]
    public async Task Siparis_verilince_sepet_bosalir()
    {
        await using var context = TestDb.NewContext();
        var (cartId, _) = await FilledCartAsync(context, quantity: 1);

        await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft);

        Assert.Empty(await new EfCartItemDal(context).GetListAsync(i => i.CartId == cartId));
    }

    [Fact]
    public async Task Bos_sepetten_siparis_olusmaz()
    {
        await using var context = TestDb.NewContext();
        var cartId = (await TestData.NewCartManager(context).GetOrCreateAsync(null)).Item2.Data!.Id;

        var (status, _) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Durum_sirayla_ilerletilir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var order = await PlaceAsync(context, quantity: 1);

        Assert.Equal(HttpStatusCode.OK, (await manager.ChangeStatusAsync(order.Id, OrderStatus.Onaylandi)).Item1);
        Assert.Equal(HttpStatusCode.OK, (await manager.ChangeStatusAsync(order.Id, OrderStatus.Kargoda)).Item1);
        Assert.Equal(HttpStatusCode.OK, (await manager.ChangeStatusAsync(order.Id, OrderStatus.TeslimEdildi)).Item1);
        Assert.Equal(OrderStatus.TeslimEdildi, (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.Status);
    }

    [Fact]
    public async Task Geri_donen_durum_degisimi_400_ile_reddedilir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var order = await PlaceAsync(context, quantity: 1);
        await manager.ChangeStatusAsync(order.Id, OrderStatus.Onaylandi);

        var (status, _) = await manager.ChangeStatusAsync(order.Id, OrderStatus.Beklemede);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(OrderStatus.Onaylandi, (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!.Status);
    }

    [Fact]
    public async Task Iptal_yalniz_beklemede_yapilir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var order = await PlaceAsync(context, quantity: 1);

        Assert.Equal(HttpStatusCode.OK, (await manager.ChangeStatusAsync(order.Id, OrderStatus.IptalEdildi)).Item1);

        var other = await PlaceAsync(context, quantity: 1, name: "Cam Sürahi", slug: "cam-surahi");
        await manager.ChangeStatusAsync(other.Id, OrderStatus.Onaylandi);

        var (status, _) = await manager.ChangeStatusAsync(other.Id, OrderStatus.IptalEdildi);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Siparis_numarasiyla_detay_okunur_bilinmeyen_numara_bulunamaz()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var order = await PlaceAsync(context, quantity: 2);

        var (foundStatus, found) = await manager.GetByOrderNoAsync(order.OrderNo);
        var (missingStatus, _) = await manager.GetByOrderNoAsync("HY-19990101-0001");

        Assert.Equal(HttpStatusCode.OK, foundStatus);
        Assert.Equal(order.OrderNo, found.Data!.Order.OrderNo);
        Assert.Single(found.Data.Items);
        Assert.Equal(HttpStatusCode.NotFound, missingStatus);
    }

    private static async Task<(Guid CartId, int ProductId)> FilledCartAsync(
        HerYerdeContext context,
        int quantity,
        decimal price = 450m,
        string name = "Çelik Tencere",
        string slug = "celik-tencere")
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, name, slug, price);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity);
        return (cartId, productId);
    }

    private static async Task<Entities.Concrete.Order> PlaceAsync(
        HerYerdeContext context,
        int quantity,
        decimal price = 450m,
        string name = "Çelik Tencere",
        string slug = "celik-tencere")
    {
        var (cartId, _) = await FilledCartAsync(context, quantity, price, name, slug);
        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft);
        Assert.Equal(HttpStatusCode.Created, status);
        return result.Data!;
    }
}
