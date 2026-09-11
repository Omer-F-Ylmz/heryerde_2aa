using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>G04: ürün sayfasında Product + BreadcrumbList, ana sayfada Organization; CSP gevşetilmez.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class JsonLdTests : IAsyncLifetime
{
    private const string Base = AdminWebFactory.BaseUrl;

    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Urun_sayfasi_gecerli_Product_tasir_fiyat_ve_stok_dogru()
    {
        await using var context = TestDb.NewContext();
        var id = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", price: 450m, campaignPrice: 399.90m, stock: 3);
        await TestData.SetDescriptionAsync(context, id, "Ağzı geniş </script><b>kalın</b> cam.");
        await TestData.AddImageAsync(context, id, "/img/cam-surahi.jpg");

        var product = PageHtml.Single(PageHtml.JsonLd(await GetHtmlAsync("/urun/cam-surahi")), "Product");

        Assert.Equal("https://schema.org", product.GetProperty("@context").GetString());
        Assert.Equal("Cam Sürahi", product.GetProperty("name").GetString());
        Assert.Equal("Ağzı geniş </script><b>kalın</b> cam.", product.GetProperty("description").GetString());
        Assert.Equal(Base + "/img/cam-surahi.jpg", product.GetProperty("image")[0].GetString());
        var offers = product.GetProperty("offers");
        Assert.Equal("Offer", offers.GetProperty("@type").GetString());
        Assert.Equal(399.90m, offers.GetProperty("price").GetDecimal());
        Assert.Equal("TRY", offers.GetProperty("priceCurrency").GetString());
        Assert.Equal("https://schema.org/InStock", offers.GetProperty("availability").GetString());
        Assert.Equal(Base + "/urun/cam-surahi", offers.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Tukenmis_urun_OutOfStock_olarak_isaretlenir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", stock: 0);

        var product = PageHtml.Single(PageHtml.JsonLd(await GetHtmlAsync("/urun/cam-surahi")), "Product");

        Assert.Equal("https://schema.org/OutOfStock", product.GetProperty("offers").GetProperty("availability").GetString());
    }

    [Fact]
    public async Task Urun_sayfasi_BreadcrumbList_kirintiyi_sirasiyla_verir()
    {
        await using var context = TestDb.NewContext();
        var child = await TestData.AddChildCategoryAsync(context, "Mutfak & Sofra", "mutfak-sofra");
        await TestData.AddProductAsync(context, child, "Cam Sürahi", "cam-surahi");

        var crumbs = PageHtml.Single(PageHtml.JsonLd(await GetHtmlAsync("/urun/cam-surahi")), "BreadcrumbList")
            .GetProperty("itemListElement").EnumerateArray().ToList();

        Assert.Equal(["Ana sayfa", "Ev", "Mutfak & Sofra", "Cam Sürahi"], crumbs.Select(c => c.GetProperty("name").GetString()));
        Assert.Equal([1, 2, 3, 4], crumbs.Select(c => c.GetProperty("position").GetInt32()));
        Assert.Equal(
            [Base + "/", Base + "/ev", Base + "/ev/mutfak-sofra", Base + "/urun/cam-surahi"],
            crumbs.Select(c => c.GetProperty("item").GetString()));
    }

    [Fact]
    public async Task Ana_sayfa_Organization_instagram_hesabini_gosterir()
    {
        var organization = PageHtml.Single(PageHtml.JsonLd(await GetHtmlAsync("/")), "Organization");

        Assert.Equal("HerYerde", organization.GetProperty("name").GetString());
        Assert.Equal(Base + "/", organization.GetProperty("url").GetString());
        Assert.Contains("https://instagram.com/heryerde_2aa", organization.GetProperty("sameAs").EnumerateArray().Select(s => s.GetString()));
    }

    [Fact]
    public async Task Json_ld_icin_csp_script_kisiti_gevsetilmez()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/urun/cam-surahi");

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self';", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.NotEmpty(PageHtml.JsonLd(await response.Content.ReadAsStringAsync()));
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
