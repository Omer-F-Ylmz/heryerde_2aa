using System.Net;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class CartManagerTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Cerezsiz_ilk_istekte_sepet_olusturulur()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);

        var (status, result) = await manager.GetOrCreateAsync(null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEqual(Guid.Empty, result.Data!.Id);
        Assert.NotNull(await new EfCartDal(context).GetAsync(c => c.Id == result.Data.Id));
    }

    [Fact]
    public async Task Ayni_urun_yeniden_eklenince_satir_birlesir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        await manager.AddAsync(cartId, productId, variantId: null, quantity: 2);
        await manager.AddAsync(cartId, productId, variantId: null, quantity: 3);

        var lines = (await manager.GetAsync(cartId)).Item2.Data!.Lines;
        Assert.Equal(5, Assert.Single(lines).Quantity);
    }

    [Fact]
    public async Task Adet_sifira_cekilince_satir_silinir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await manager.AddAsync(cartId, productId, variantId: null, quantity: 2);
        var itemId = (await manager.GetAsync(cartId)).Item2.Data!.Lines[0].ItemId;

        var (status, _) = await manager.SetQuantityAsync(cartId, itemId, 0);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty((await manager.GetAsync(cartId)).Item2.Data!.Lines);
    }

    [Fact]
    public async Task Kampanyali_urun_kampanya_fiyatiyla_donar_ve_sonraki_fiyat_degisimi_satiri_etkilemez()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m, campaignPrice: 400m);
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        await manager.AddAsync(cartId, productId, variantId: null, quantity: 1);

        var product = (await new EfProductDal(context).GetTrackedAsync(p => p.Id == productId))!;
        product.Price = 900m;
        product.CampaignPrice = null;
        await new EfUnitOfWork(context).SaveChangesAsync();

        var line = (await manager.GetAsync(cartId)).Item2.Data!.Lines[0];
        Assert.Equal(400m, line.UnitPrice);
    }

    [Fact]
    public async Task Sepet_toplami_ara_toplam_arti_kargodur()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m);
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await manager.AddAsync(cartId, productId, variantId: null, quantity: 2);

        var view = (await manager.GetAsync(cartId)).Item2.Data!;

        Assert.Equal(900m, view.Subtotal);
        Assert.Equal(TestData.ShippingFee, view.ShippingFee);
        Assert.Equal(900m + TestData.ShippingFee, view.Total);
    }

    [Fact]
    public async Task Bos_sepette_kargo_ucreti_yazilmaz()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        var view = (await manager.GetAsync(cartId)).Item2.Data!;

        Assert.Equal(0m, view.ShippingFee);
        Assert.Equal(0m, view.Total);
    }

    [Fact]
    public async Task Varyantli_urunde_satir_beden_ve_renk_tasir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var (productId, variantId) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", size: "M", color: "Kiremit", stock: 5);
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        await manager.AddAsync(cartId, productId, variantId, quantity: 1);

        var line = (await manager.GetAsync(cartId)).Item2.Data!.Lines[0];
        Assert.Equal("M", line.Size);
        Assert.Equal("Kiremit", line.Color);
        Assert.Equal(5, line.AvailableStock);
    }
}
