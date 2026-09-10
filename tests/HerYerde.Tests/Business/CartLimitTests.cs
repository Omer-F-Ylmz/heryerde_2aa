using System.Net;

namespace HerYerde.Tests.Business;

/// <summary>B05/B06/B09: satır adedi 1..MaxQtyPerLine, varyantlıda ayrıca stok kadar.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CartLimitTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Bir_altindaki_adet_reddedilir(int quantity)
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        var (status, _) = await manager.AddAsync(cartId, productId, variantId: null, quantity);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Empty((await manager.GetAsync(cartId)).Item2.Data!.Lines);
    }

    [Fact]
    public async Task Ust_sinira_esit_adet_kabul_edilir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        var (status, _) = await manager.AddAsync(cartId, productId, variantId: null, TestData.MaxQtyPerLine);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(TestData.MaxQtyPerLine, (await manager.GetAsync(cartId)).Item2.Data!.Lines[0].Quantity);
    }

    [Fact]
    public async Task Ust_siniri_asan_adet_mesajla_reddedilir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        var (status, result) = await manager.AddAsync(cartId, productId, variantId: null, TestData.MaxQtyPerLine + 1);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(TestData.MaxQtyPerLine.ToString(), result.Message);
    }

    [Fact]
    public async Task Satir_birlesirken_de_ust_sinir_asilamaz()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await manager.AddAsync(cartId, productId, variantId: null, TestData.MaxQtyPerLine);

        var (status, _) = await manager.AddAsync(cartId, productId, variantId: null, 1);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(TestData.MaxQtyPerLine, (await manager.GetAsync(cartId)).Item2.Data!.Lines[0].Quantity);
    }

    [Fact]
    public async Task Adet_guncellemede_de_ust_sinir_gecerlidir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await manager.AddAsync(cartId, productId, variantId: null, 1);
        var itemId = (await manager.GetAsync(cartId)).Item2.Data!.Lines[0].ItemId;

        var (status, _) = await manager.SetQuantityAsync(cartId, itemId, TestData.MaxQtyPerLine + 1);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(1, (await manager.GetAsync(cartId)).Item2.Data!.Lines[0].Quantity);
    }

    [Fact]
    public async Task Stogu_biten_varyant_sepete_eklenemez()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var (productId, variantId) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 0);
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        var (status, _) = await manager.AddAsync(cartId, productId, variantId, quantity: 1);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Empty((await manager.GetAsync(cartId)).Item2.Data!.Lines);
    }

    [Fact]
    public async Task Varyant_stogundan_fazlasi_sepete_eklenemez()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewCartManager(context);
        var (productId, variantId) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 2);
        var cartId = (await manager.GetOrCreateAsync(null)).Item2.Data!.Id;

        var (status, _) = await manager.AddAsync(cartId, productId, variantId, quantity: 3);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Empty((await manager.GetAsync(cartId)).Item2.Data!.Lines);
    }
}
