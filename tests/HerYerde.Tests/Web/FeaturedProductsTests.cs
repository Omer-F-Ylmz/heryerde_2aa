using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;

namespace HerYerde.Tests.Web;

/// <summary>D14 B3: ana sayfada "Yeni gelenler" yerine "Öne çıkanlar"; işaretli ürün yoksa yeniye düşer.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class FeaturedProductsTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task One_cikan_yoksa_ana_sayfa_yeni_gelenleri_gosterir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("Yeni gelenler", html);
        Assert.DoesNotContain("Öne çıkanlar", html);
        Assert.Contains("Çelik Tencere", html);
    }

    [Fact]
    public async Task One_cikanlar_siraya_gore_yeni_gelenlerin_yerini_alir()
    {
        await using var context = TestDb.NewContext();
        // Eklenme sırası ile öne çıkan sırası kasten ters: sıralamanın featured_order'dan geldiği görülsün.
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", stock: 5);
        await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", stock: 5);
        await FeatureAsync(context, "celik-tencere", 2);
        await FeatureAsync(context, "cam-surahi", 1);

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("Öne çıkanlar", html);
        Assert.DoesNotContain("Yeni gelenler", html);
        // İşaretsiz ürün listede yok; işaretliler sıra numarasına göre.
        Assert.DoesNotContain("Hasır Sepet", html);
        Assert.True(html.IndexOf("Cam Sürahi", StringComparison.Ordinal) < html.IndexOf("Çelik Tencere", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Yayindan_kaldirilan_one_cikan_ana_sayfada_gorunmez()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", stock: 5);
        await FeatureAsync(context, "celik-tencere", 1);
        await FeatureAsync(context, "cam-surahi", 2, active: false);

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("Öne çıkanlar", html);
        Assert.Contains("Çelik Tencere", html);
        Assert.DoesNotContain("Cam Sürahi", html);
    }

    private static async Task FeatureAsync(HerYerdeContext context, string slug, int order, bool active = true)
    {
        var dal = new EfProductDal(context);
        var product = (await dal.GetTrackedAsync(p => p.Slug == slug))!;
        product.IsFeatured = true;
        product.FeaturedOrder = order;
        product.IsActive = active;
        await new EfUnitOfWork(context).SaveChangesAsync();
    }
}
