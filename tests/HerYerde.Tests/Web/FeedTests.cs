using System.Net;
using System.Xml.Linq;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Web;

/// <summary>D14 B6: Meta katalog ve Google Merchant ürün akışları; yalnız yayındaki, fiyatı olan ürünler.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeedTests : IAsyncLifetime
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
    public async Task Meta_akisi_gecerli_xml_ve_yalniz_yayindaki_urunleri_verir()
    {
        await using var context = TestDb.NewContext();
        var visible = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 1290.90m, stock: 5);
        await TestData.AddImageAsync(context, visible, "/uploads/products/1/abc-800.webp");
        var hidden = await TestData.AddHomeProductAsync(context, "Gizli Ürün", "gizli-urun", price: 500m, stock: 5);
        await HideAsync(context, hidden);

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/feeds/meta.xml");
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
        var items = xml.Root!.Element("channel")!.Elements("item").ToList();
        var item = Assert.Single(items);
        Assert.Equal("celik-tencere", item.Element(G + "id")!.Value);
        Assert.Equal("Çelik Tencere", item.Element(G + "title")!.Value);
        Assert.Equal($"{TestData.BaseUrl}/urun/celik-tencere", item.Element(G + "link")!.Value);
        Assert.Equal($"{TestData.BaseUrl}/uploads/products/1/abc-800.webp", item.Element(G + "image_link")!.Value);
        Assert.Equal("in stock", item.Element(G + "availability")!.Value);
        Assert.Equal("1290.90 TRY", item.Element(G + "price")!.Value);
        Assert.Equal("new", item.Element(G + "condition")!.Value);
        Assert.Equal("HerYerde", item.Element(G + "brand")!.Value);
        Assert.NotEmpty(item.Element(G + "description")!.Value);
        Assert.NotEmpty(item.Element(G + "google_product_category")!.Value);
    }

    [Fact]
    public async Task Fiyatsiz_ve_stoksuz_urunler_dogru_islenir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m, stock: 0);
        var free = await TestData.AddHomeProductAsync(context, "Fiyatsız Ürün", "fiyatsiz-urun", price: 0m, stock: 5);
        Assert.True(free > 0);

        var xml = XDocument.Parse(await (await _factory.CreateNonRedirectingClient().GetAsync("/feeds/meta.xml")).Content.ReadAsStringAsync());
        var items = xml.Root!.Element("channel")!.Elements("item").ToList();

        // Fiyatı olmayan ürün akışa girmez; stoğu biten "out of stock" ile kalır.
        var item = Assert.Single(items);
        Assert.Equal("celik-tencere", item.Element(G + "id")!.Value);
        Assert.Equal("out of stock", item.Element(G + "availability")!.Value);
    }

    [Fact]
    public async Task Varyantli_urun_her_varyant_icin_item_group_id_ile_cikar()
    {
        await using var context = TestDb.NewContext();
        var (productId, _) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", size: "M", stock: 3, price: 450m);
        await new EfProductVariantDal(context).AddAsync(new ProductVariant
        {
            ProductId = productId,
            Size = "L",
            Color = "Kiremit",
            Sku = "SALVAR-L",
            Stock = 0
        });
        await new EfUnitOfWork(context).SaveChangesAsync();

        var xml = XDocument.Parse(await (await _factory.CreateNonRedirectingClient().GetAsync("/feeds/meta.xml")).Content.ReadAsStringAsync());
        var items = xml.Root!.Element("channel")!.Elements("item").ToList();

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("salvar", i.Element(G + "item_group_id")!.Value));
        Assert.Contains(items, i => i.Element(G + "id")!.Value == "SALVAR-M" && i.Element(G + "availability")!.Value == "in stock");
        Assert.Contains(items, i => i.Element(G + "id")!.Value == "SALVAR-L" && i.Element(G + "availability")!.Value == "out of stock");
    }

    [Fact]
    public async Task Google_akisi_fiyati_TRY_bicimiyle_verir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 1500m, campaignPrice: 1290.90m, stock: 5);

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/feeds/google.xml");
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(xml.Root!.Element("channel")!.Elements("item"));
        // Kampanya sürerken liste fiyatı price, kampanya fiyatı sale_price olur.
        Assert.Equal("1500.00 TRY", item.Element(G + "price")!.Value);
        Assert.Equal("1290.90 TRY", item.Element(G + "sale_price")!.Value);
    }

    [Fact]
    public async Task Robots_akislari_tarayiciya_acar()
    {
        var robots = await (await _factory.CreateNonRedirectingClient().GetAsync("/robots.txt")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Disallow: /feeds", robots);
        Assert.Contains("Allow: /feeds/", robots);
    }

    private static async Task HideAsync(HerYerdeContext context, int productId)
    {
        var product = (await new EfProductDal(context).GetTrackedAsync(p => p.Id == productId))!;
        product.IsActive = false;
        await new EfUnitOfWork(context).SaveChangesAsync();
    }
}
