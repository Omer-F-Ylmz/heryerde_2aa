using System.Net;
using HerYerde.Business;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>KAPANIŞ-5: ödemede ön bilgilendirme + mesafeli satış onayı zorunlu; sipariş onay anını ve metin sürümünü saklar.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CheckoutConsentTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Odeme_formu_zorunlu_onay_kutusunu_sozlesme_baglantilariyla_cizer()
    {
        await using var context = TestDb.NewContext();
        var client = await ClientWithCartAsync(context);

        var html = await (await client.GetAsync("/odeme")).Content.ReadAsStringAsync();

        Assert.Matches("<input[^>]*type=\"checkbox\"[^>]*name=\"LegalConsent\"[^>]*required", html);
        Assert.Contains("href=\"/yasal/on-bilgilendirme-formu\"", html);
        Assert.Contains("href=\"/yasal/mesafeli-satis-sozlesmesi\"", html);
    }

    [Fact]
    public async Task Onay_kutusu_isaretlenmeden_gonderilen_odeme_400_ve_siparis_acilmaz()
    {
        await using var context = TestDb.NewContext();
        var client = await ClientWithCartAsync(context);

        var response = await HtmlForm.PostAsync(client, "/odeme", "/odeme", Form(consent: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await new EfOrderDal(context).GetListAsync());
        Assert.Contains("mesafeli satış sözleşmesini onaylayın", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Onayli_sipariste_onay_ani_ve_sozlesme_surumu_yazilir()
    {
        await using var context = TestDb.NewContext();
        var client = await ClientWithCartAsync(context);

        var response = await HtmlForm.PostAsync(client, "/odeme", "/odeme", Form(consent: true));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var order = Assert.Single(await new EfOrderDal(context).GetListAsync());
        Assert.Equal(TestClock.Now, order.ConsentAt);
        Assert.Equal(LegalDocs.Version, order.LegalVersion);
    }

    [Fact]
    public async Task Tesekkur_sayfasi_ve_yonetim_detayi_sozlesmeye_baglanir()
    {
        await using var context = TestDb.NewContext();
        var client = await ClientWithCartAsync(context);
        var placed = await HtmlForm.PostAsync(client, "/odeme", "/odeme", Form(consent: true));
        var order = Assert.Single(await new EfOrderDal(context).GetListAsync());

        var thanks = await (await client.GetAsync(placed.Headers.Location!.OriginalString)).Content.ReadAsStringAsync();
        var admin = await (await (await _factory.CreateSignedInClientAsync()).GetAsync($"/admin/orders/detail/{order.Id}"))
            .Content.ReadAsStringAsync();

        foreach (var html in new[] { thanks, admin })
        {
            Assert.Contains("href=\"/yasal/mesafeli-satis-sozlesmesi\"", html);
            Assert.Contains(LegalDocs.Version, html);
        }
    }

    private async Task<HttpClient> ClientWithCartAsync(HerYerdeContext context)
    {
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m);
        var client = _factory.CreateNonRedirectingClient();
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });
        return client;
    }

    private static Dictionary<string, string> Form(bool consent)
    {
        var fields = new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)PaymentMethod.KapidaOdeme).ToString()
        };

        if (consent)
        {
            fields["LegalConsent"] = "true";
        }

        return fields;
    }
}
