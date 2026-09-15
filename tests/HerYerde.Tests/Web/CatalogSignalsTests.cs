using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D10 A1/A3: varyantlı Ev ürün sayfası, "Son N adet" rozeti, müşteri durum çizgisi, yönetim düşük stok sayacı.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CatalogSignalsTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Varyantli_Ev_urununde_eksen_etiketleri_secicide_gorunur_secimsiz_sepet_kapali()
    {
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Keten Masa Örtüsü", "keten-masa-ortusu");
            var product = await context.Products.SingleAsync(p => p.Id == productId);
            product.VariantAxis1Label = "Boy";
            product.VariantAxis2Label = "Desen";
            context.ProductVariants.Add(new ProductVariant { ProductId = productId, Size = "160x220", Color = "Çizgili", Sku = "KMO-1", Stock = 6 });
            await context.SaveChangesAsync();
        }

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/urun/keten-masa-ortusu")).Content.ReadAsStringAsync();

        Assert.Contains("<legend class=\"chips__legend\">Boy</legend>", html);
        Assert.Contains("<legend class=\"chips__legend\">Desen</legend>", html);
        Assert.DoesNotContain(">Beden</legend>", html);
        Assert.Matches(new Regex("<button[^>]*disabled[^>]*data-add-button"), html);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task Son_adet_rozeti_esik_altinda_gorunur_ustunde_gorunmez(int stock, bool shown)
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: stock);
        }

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();

        Assert.Equal(shown, html.Contains($"Son {stock} adet", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Musteri_siparis_sayfasinda_bes_adimli_durum_cizgisi_gecerli_adimi_isaretler()
    {
        Order order;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            var cart = TestData.NewCartManager(context);
            var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
            await cart.AddAsync(cartId, productId, null, 1);
            var manager = TestData.NewOrderManager(context);
            order = (await manager.PlaceAsync(cartId, new OrderDraft("Ayşe Yılmaz", "05001234567", null, "Örnek mah. 1", "İstanbul", "Kadıköy", null, PaymentMethod.KapidaOdeme))).Item2.Data!;
            await manager.ChangeStatusAsync(order.Id, OrderStatus.Onaylandi);
            await manager.ChangeStatusAsync(order.Id, OrderStatus.Hazirlaniyor);
        }

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}")).Content.ReadAsStringAsync();

        var steps = Regex.Matches(html, "<li class=\"status-steps__item([^\"]*)\"[^>]*>(.*?)</li>", RegexOptions.Singleline);
        Assert.Equal(5, steps.Count);
        Assert.Equal(["Alındı", "Onaylandı", "Hazırlanıyor", "Kargoda", "Teslim edildi"], steps.Select(s => Regex.Replace(s.Groups[2].Value, "<[^>]+>", "").Trim()));
        Assert.Contains("is-current", steps[2].Groups[1].Value);
        Assert.Contains("is-done", steps[1].Groups[1].Value);
        Assert.DoesNotContain("is-", steps[3].Groups[1].Value);
        Assert.Contains("aria-current=\"step\"", steps[2].Value);
    }

    [Fact]
    public async Task Yonetimde_dusuk_stok_sayaci_ve_stok_listesi_esigi_uygular()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
            await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", stock: 6);
            await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet");
            await TestData.AddClothingProductAsync(context, "Eşarp", "esarp", size: "Std", color: "Mavi", stock: 2);
        }

        var client = await _factory.CreateSignedInClientAsync();
        var list = await client.GetAsync("/admin/stok");
        var html = await list.Content.ReadAsStringAsync();
        var layout = await (await client.GetAsync("/admin/products")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("Çelik Tencere", html);
        Assert.Contains("ESARP-Std", html);
        Assert.DoesNotContain("Cam Sürahi", html);
        Assert.DoesNotContain("Hasır Sepet", html);
        Assert.Matches(new Regex("data-low-stock-count[^>]*>2<"), layout);
    }
}
