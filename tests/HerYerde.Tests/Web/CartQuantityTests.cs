using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;

namespace HerYerde.Tests.Web;

/// <summary>B05/B09: sepet formu geçersiz adedi sessizce yuvarlamaz, 400 döner.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CartQuantityTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>KAPANIŞ-2 ZAP 90022: Location başlığı yalnız ASCII taşıyabilir; ASCII dışı dönüş adresi
    /// Kestrel'de 500 üretiyordu, sepet sayfasına düşülür.</summary>
    [Theory]
    [InlineData("/ş")]
    [InlineData("/sepet\u2028")]
    public async Task Ascii_disi_donus_adresi_sepete_yonlenir(string donus)
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = _factory.CreateNonRedirectingClient();

        var response = await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1",
            ["donus"] = donus
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/sepet", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("11")]
    public async Task Aralik_disi_adet_400_doner_ve_sepete_yazilmaz(string quantity)
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = _factory.CreateNonRedirectingClient();

        var response = await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = quantity
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await new EfCartItemDal(context).GetListAsync());
    }

    [Fact]
    public async Task Ust_sinira_esit_adet_sepete_eklenir()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = _factory.CreateNonRedirectingClient();

        var response = await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "10"
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(10, Assert.Single(await new EfCartItemDal(context).GetListAsync()).Quantity);
    }
}
