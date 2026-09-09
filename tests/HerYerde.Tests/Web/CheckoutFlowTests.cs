using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

[Collection(DatabaseCollection.Name)]
public sealed class CheckoutFlowTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Sepete_ekleyince_sepet_cerezi_verilir_ve_sepet_olusur()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var productId = (await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere"))!.Id;
        var client = _factory.CreateNonRedirectingClient();

        var response = await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("heryerde.cart"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await new EfCartDal(context).GetListAsync());
        Assert.Single(await new EfCartItemDal(context).GetListAsync());
    }

    [Fact]
    public async Task Bos_sepette_odeme_sayfasi_sepete_yonlendirir()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync("/odeme");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/sepet", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Baskasinin_siparis_numarasiyla_tesekkur_sayfasi_acilmaz()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync("/siparis/HY-19990101-0001/tesekkur");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sepetten_odemeye_siparis_tamamlanir_ve_tesekkur_sayfasi_ozet_gosterir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m);
        var productId = (await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere"))!.Id;
        var client = _factory.CreateNonRedirectingClient();

        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "2"
        });

        var placed = await HtmlForm.PostAsync(client, "/odeme", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)PaymentMethod.KapidaOdeme).ToString()
        });

        Assert.Equal(HttpStatusCode.Found, placed.StatusCode);
        var order = Assert.Single(await new EfOrderDal(context).GetListAsync());
        Assert.Equal($"/siparis/{order.OrderNo}/tesekkur", placed.Headers.Location!.OriginalString);

        var html = await (await client.GetAsync(placed.Headers.Location.OriginalString)).Content.ReadAsStringAsync();
        Assert.Contains(order.OrderNo, html);
        Assert.Contains("wa.me", html);
    }

    [Fact]
    public async Task Gecersiz_telefon_odeme_formunda_hata_ile_kalir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var productId = (await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere"))!.Id;
        var client = _factory.CreateNonRedirectingClient();
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });

        var response = await HtmlForm.PostAsync(client, "/odeme", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0212 497 09 82",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)PaymentMethod.KapidaOdeme).ToString()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await new EfOrderDal(context).GetListAsync());
    }
}
