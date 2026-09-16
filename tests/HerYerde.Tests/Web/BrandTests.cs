using System.Globalization;
using System.Net;
using System.Xml.Linq;
using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D15 B1: marka. Ürün markaya bağlanır, /marka/{slug} markanın ürünlerini listeler, akışlar ürünün markasını
/// yazar ve ürün adı önerisi marka yazımını marka tablosundan alır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class BrandTests : IAsyncLifetime
{
    private static readonly XNamespace G = "http://base.google.com/ns/1.0";

    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Marka_sayfasi_yalniz_markanin_urunlerini_listeler()
    {
        await using (var context = TestDb.NewContext())
        {
            var tac = await TestData.AddBrandAsync(context, "TAÇ", "tac");
            var karaca = await TestData.AddBrandAsync(context, "Karaca", "karaca");
            await TestData.SetBrandAsync(context, await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere"), tac);
            await TestData.SetBrandAsync(context, await TestData.AddHomeProductAsync(context, "Döküm Tava", "dokum-tava"), karaca);
            await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet");
        }

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/marka/tac");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1", html);
        Assert.Contains("TAÇ", html);
        Assert.Contains("/urun/celik-tencere", html);
        Assert.DoesNotContain("/urun/dokum-tava", html);
        Assert.DoesNotContain("/urun/hasir-sepet", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://heryerde.test/marka/tac\"", html);
    }

    [Fact]
    public async Task Bilinmeyen_marka_404_alir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/marka/yok-boyle");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Akista_marka_urunun_markasi_markasiz_urunde_magaza_adi()
    {
        await using (var context = TestDb.NewContext())
        {
            var tac = await TestData.AddBrandAsync(context, "TAÇ", "tac");
            var branded = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 900m, stock: 3);
            await TestData.SetBrandAsync(context, branded, tac);
            await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", price: 300m, stock: 3);
        }

        var xml = XDocument.Parse(await (await _factory.CreateNonRedirectingClient().GetAsync("/feeds/google.xml")).Content.ReadAsStringAsync());
        var brands = xml.Root!.Element("channel")!.Elements("item")
            .ToDictionary(i => i.Element(G + "title")!.Value, i => i.Element(G + "brand")!.Value);

        Assert.Equal("TAÇ", brands["Çelik Tencere"]);
        Assert.Equal("HerYerde", brands["Hasır Sepet"]);
    }

    [Fact]
    public async Task Yonetimden_marka_eklenir_ve_urune_atanir()
    {
        int productId;
        int categoryId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            categoryId = (await new EfProductDal(context).GetAsync(p => p.Id == productId))!.CategoryId;
        }

        var client = await _factory.CreateSignedInClientAsync();
        var created = await HtmlForm.PostAsync(client, "/admin/markalar/yeni", "/admin/markalar/yeni", new Dictionary<string, string>
        {
            ["Name"] = "Emsan Home"
        });
        Assert.Equal(HttpStatusCode.Found, created.StatusCode);

        int brandId;
        await using (var context = TestDb.NewContext())
        {
            var brand = await context.Brands.SingleAsync();
            Assert.Equal(("Emsan Home", "emsan-home"), (brand.Name, brand.Slug));
            brandId = brand.Id;
        }

        var form = await (await client.GetAsync($"/admin/products/edit/{productId}")).Content.ReadAsStringAsync();
        Assert.Contains($"<option value=\"{brandId}\">Emsan Home</option>", form);

        var saved = await HtmlForm.PostAsync(client, $"/admin/products/edit/{productId}", "/admin/products/edit", new Dictionary<string, string>
        {
            ["Id"] = productId.ToString(CultureInfo.InvariantCulture),
            ["Name"] = "Çelik Tencere",
            ["Description"] = "Çok katmanlı taban.",
            ["CategoryId"] = categoryId.ToString(CultureInfo.InvariantCulture),
            ["BrandId"] = brandId.ToString(CultureInfo.InvariantCulture),
            ["Price"] = "500.00",
            ["IsActive"] = "true"
        });

        Assert.Equal(HttpStatusCode.Found, saved.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal(brandId, (await check.Products.SingleAsync()).BrandId);
    }

    [Fact]
    public async Task Urun_adi_onerisi_marka_yazimini_marka_tablosundan_alir()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            // Sabit listede olmayan bir marka, cümlenin ortasında: yazımı yalnız marka tablosundan gelebilir.
            await TestData.AddBrandAsync(context, "Karaca", "karaca");
            productId = await TestData.AddHomeProductAsync(context, "DÖKÜM TAVA KARACA 28 CM", "dokum-tava-karaca-28-cm");
        }

        var client = await _factory.CreateSignedInClientAsync();
        var form = await (await client.GetAsync($"/admin/products/edit/{productId}")).Content.ReadAsStringAsync();

        Assert.Contains("Döküm tava Karaca 28 cm", form);
    }
}
