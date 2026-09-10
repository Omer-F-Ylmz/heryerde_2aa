using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>B01/B02: sipariş numarası ve stok düşümü yarışa dayanmalı.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderConcurrencyTests : IAsyncLifetime
{
    private static readonly OrderDraft Draft = new(
        "Ayşe Yılmaz",
        "0542 497 09 82",
        null,
        "Cumhuriyet Mah. 12/3",
        "İstanbul",
        "Kadıköy",
        null,
        PaymentMethod.KapidaOdeme);

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Alti_es_zamanli_siparis_alti_farkli_numara_uretir_ve_hicbiri_patlamaz()
    {
        int productId;
        await using (var setup = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(setup, "Cam Sürahi", "cam-surahi");
        }

        var cartIds = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            await using var setup = TestDb.NewContext();
            cartIds.Add(await FilledCartAsync(setup, productId, variantId: null, quantity: 1));
        }

        var results = await Task.WhenAll(cartIds.Select(PlaceInOwnContextAsync));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.Status));
        var numbers = results.Select(r => r.OrderNo!).ToList();
        Assert.Equal(6, numbers.Distinct().Count());
    }

    [Fact]
    public async Task Es_zamanli_iki_siparis_stok_ucken_ikiser_adet_isterse_biri_409_alir()
    {
        int productId;
        int variantId;
        await using (var setup = TestDb.NewContext())
        {
            (productId, variantId) = await TestData.AddClothingProductAsync(setup, "Şalvar", "salvar", stock: 3);
        }

        var cartIds = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            await using var setup = TestDb.NewContext();
            cartIds.Add(await FilledCartAsync(setup, productId, variantId, quantity: 2));
        }

        var results = await Task.WhenAll(cartIds.Select(PlaceInOwnContextAsync));

        Assert.Single(results, r => r.Status == HttpStatusCode.Created);
        Assert.Single(results, r => r.Status == HttpStatusCode.Conflict);

        await using var check = TestDb.NewContext();
        var variant = await new EfProductVariantDal(check).GetAsync(v => v.Id == variantId);
        Assert.Equal(1, variant!.Stock);
    }

    [Fact]
    public async Task Stok_dusumu_arada_olunca_yonetici_kaydi_409_ile_reddedilir()
    {
        int variantId;
        await using (var setup = TestDb.NewContext())
        {
            (_, variantId) = await TestData.AddClothingProductAsync(setup, "Şalvar", "salvar", stock: 3);
        }

        await using var adminContext = TestDb.NewContext();
        var adminDal = new EfProductVariantDal(adminContext);
        // Yönetici formu varyantı okudu: damga bu andan.
        Assert.NotNull(await adminDal.GetTrackedAsync(v => v.Id == variantId));

        await using (var buyer = TestDb.NewContext())
        {
            Assert.Equal(1, await new EfProductVariantDal(buyer).TryDecrementStockAsync(variantId, 1));
        }

        var (status, _) = await TestData.NewProductManager(adminContext).UpdateStockAsync(variantId, 10);

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task Stok_yetmiyorsa_kosullu_dusum_satir_etkilemez()
    {
        int variantId;
        await using (var setup = TestDb.NewContext())
        {
            (_, variantId) = await TestData.AddClothingProductAsync(setup, "Şalvar", "salvar", stock: 2);
        }

        await using var context = TestDb.NewContext();
        var dal = new EfProductVariantDal(context);

        Assert.Equal(0, await dal.TryDecrementStockAsync(variantId, 3));
        Assert.Equal(2, (await dal.GetAsync(v => v.Id == variantId))!.Stock);
    }

    private static async Task<(HttpStatusCode Status, string? OrderNo)> PlaceInOwnContextAsync(Guid cartId)
    {
        await using var context = TestDb.NewContext();
        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, Draft);
        return (status, result.Data?.OrderNo);
    }

    private static async Task<Guid> FilledCartAsync(HerYerdeContext context, int productId, int? variantId, int quantity)
    {
        var cart = new Cart { Id = Guid.NewGuid(), CreatedAt = TestClock.Now };
        await new EfCartDal(context).AddAsync(cart);
        await new EfCartItemDal(context).AddAsync(new CartItem
        {
            CartId = cart.Id,
            ProductId = productId,
            VariantId = variantId,
            Quantity = quantity,
            UnitPrice = 450m
        });
        await new EfUnitOfWork(context).SaveChangesAsync();
        return cart.Id;
    }
}
