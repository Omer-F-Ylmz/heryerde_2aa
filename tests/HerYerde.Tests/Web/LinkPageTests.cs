using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;

namespace HerYerde.Tests.Web;

/// <summary>D14 B1: Instagram profil bağlantısı için /link sayfası; mobil-öncelikli tek sütun, dizine eklenmez.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class LinkPageTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Link_sayfasi_acilir_ve_dizine_eklenmez()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/link");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"robots\" content=\"noindex", html);
        Assert.Contains("/ev", html);
        Assert.Contains("/ortu", html);
        Assert.Contains("/siparis-sorgula", html);
        Assert.Contains("wa.me", html);
    }

    [Fact]
    public async Task Link_sayfasi_one_cikan_dort_urunu_gosterir()
    {
        await using var context = TestDb.NewContext();
        foreach (var (name, slug, order) in new[]
                 {
                     ("Çelik Tencere", "celik-tencere", 1),
                     ("Cam Sürahi", "cam-surahi", 2),
                     ("Hasır Sepet", "hasir-sepet", 3),
                     ("Ahşap Kaşık", "ahsap-kasik", 4),
                     ("Beşinci Ürün", "besinci-urun", 5)
                 })
        {
            await TestData.AddHomeProductAsync(context, name, slug, stock: 5);
            await FeatureAsync(context, slug, order);
        }

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/link")).Content.ReadAsStringAsync();

        Assert.Contains("Çelik Tencere", html);
        Assert.Contains("Ahşap Kaşık", html);
        // En çok dört öne çıkan; beşincisi listeye girmez.
        Assert.DoesNotContain("Beşinci Ürün", html);
    }

    [Fact]
    public async Task Link_sayfasi_sitemapte_yok()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);

        var sitemap = await (await _factory.CreateNonRedirectingClient().GetAsync("/sitemap.xml")).Content.ReadAsStringAsync();
        var robots = await (await _factory.CreateNonRedirectingClient().GetAsync("/robots.txt")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("/link", sitemap);
        Assert.Contains("Disallow: /link", robots);
    }

    private static async Task FeatureAsync(HerYerdeContext context, string slug, int order)
    {
        var product = (await new EfProductDal(context).GetTrackedAsync(p => p.Slug == slug))!;
        product.IsFeatured = true;
        product.FeaturedOrder = order;
        await new EfUnitOfWork(context).SaveChangesAsync();
    }
}
