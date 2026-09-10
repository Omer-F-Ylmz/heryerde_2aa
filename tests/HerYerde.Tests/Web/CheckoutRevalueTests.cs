using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;

namespace HerYerde.Tests.Web;

/// <summary>B07: sepetteki fiyat donmaz; ödeme adımında yeniden değerlenir ve değişim kullanıcıya söylenir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CheckoutRevalueTests : IAsyncLifetime
{
    private const string Notice = "fiyat";

    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Kampanya_bitince_odeme_sayfasi_fiyati_yeniler_ve_uyarir()
    {
        await using var context = TestDb.NewContext();
        var client = await CartWithExpiredCampaignAsync(context);

        var response = await client.GetAsync("/odeme");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(Notice, html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(450m, Assert.Single(await new EfCartItemDal(context).GetListAsync()).UnitPrice);
    }

    [Fact]
    public async Task Fiyat_degistiyse_odeme_gonderimi_siparis_acmadan_sepete_doner()
    {
        await using var context = TestDb.NewContext();
        var client = await CartWithExpiredCampaignAsync(context);

        // Jeton sepetten alınır: /odeme GET'i zaten yeniden değerleyeceği için POST korumasını gölgelerdi.
        var response = await HtmlForm.PostAsync(client, "/sepet", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = "1"
        });

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("/sepet", response.Headers.Location!.OriginalString);
        Assert.Empty(await new EfOrderDal(context).GetListAsync());

        var cart = await (await client.GetAsync("/sepet")).Content.ReadAsStringAsync();
        Assert.Contains(Notice, cart, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fiyat_ayniysa_odeme_gonderimi_siparisi_olusturur()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m);
        var client = _factory.CreateNonRedirectingClient();
        await AddToCartAsync(client, productId);

        var response = await HtmlForm.PostAsync(client, "/odeme", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = "1"
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Single(await new EfOrderDal(context).GetListAsync());
    }

    /// <summary>Kampanyalı fiyattan sepete atılmış, ardından kampanyası biten tek satırlık sepet.</summary>
    private async Task<HttpClient> CartWithExpiredCampaignAsync(HerYerdeContext context)
    {
        var productId = await TestData.AddHomeProductAsync(
            context,
            "Çelik Tencere",
            "celik-tencere",
            price: 450m,
            campaignPrice: 400m);

        var client = _factory.CreateNonRedirectingClient();
        await AddToCartAsync(client, productId);

        var product = (await new EfProductDal(context).GetTrackedAsync(p => p.Id == productId))!;
        product.CampaignEndsAt = TestClock.Now.AddDays(-1);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return client;
    }

    private static async Task AddToCartAsync(HttpClient client, int productId)
    {
        var added = await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });

        Assert.Equal(HttpStatusCode.Found, added.StatusCode);
    }
}
