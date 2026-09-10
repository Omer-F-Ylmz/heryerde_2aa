using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;

namespace HerYerde.Tests.Web;

/// <summary>G10: /ara — ad, açıklama ve kategori adında LIKE; kısa terim mesaj sayfası döner.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SearchTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Tek_karakterlik_terim_400_degil_mesaj_sayfasi_doner()
    {
        var html = await GetAsync("/ara?q=a");

        Assert.Contains("en az 2 harf", html);
        Assert.DoesNotContain("class=\"card\"", html);
    }

    [Fact]
    public async Task Aciklamada_gecen_kelime_urunu_bulur()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Granit Tava", "granit-tava");
        await SetDescriptionAsync(context, "granit-tava", "Çeyizlik döküm gövde, indüksiyon uyumlu.");

        var html = await GetAsync("/ara?q=ind%C3%BCksiyon");

        Assert.Contains("Granit Tava", html);
    }

    [Fact]
    public async Task Kategori_adinda_gecen_kelime_urunu_bulur()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Granit Tava", "granit-tava");

        var html = await GetAsync("/ara?q=ev");

        Assert.Contains("Granit Tava", html);
    }

    [Fact]
    public async Task Silinen_urun_sonuclarda_gorunmez()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Granit Tava", "granit-tava");
        await TestData.NewProductManager(context).DeleteAsync(productId);

        var html = await GetAsync("/ara?q=granit");

        Assert.DoesNotContain("Granit Tava", html);
        Assert.Contains("0 sonuç", html);
    }

    [Fact]
    public async Task Sayfa_basina_24_urun_listelenir_ve_ikinci_sayfa_kalani_gosterir()
    {
        await using var context = TestDb.NewContext();
        for (var i = 1; i <= 26; i++)
        {
            await TestData.AddHomeProductAsync(context, $"Sepet {i:00}", $"sepet-{i:00}");
        }

        var first = await GetAsync("/ara?q=sepet");
        var second = await GetAsync("/ara?q=sepet&sayfa=2");

        Assert.Equal(24, CardCount(first));
        Assert.Equal(2, CardCount(second));
        Assert.Contains("26 sonuç", first);
    }

    [Fact]
    public async Task Sonuc_yoksa_bos_durum_ve_yeni_gelenler_cikar()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Granit Tava", "granit-tava");

        var html = await GetAsync("/ara?q=zzzzq");

        Assert.Contains("Bu aramaya uyan ürün bulamadık", html);
        Assert.Contains("Yeni gelenler", html);
        Assert.Contains("Granit Tava", html);
    }

    [Fact]
    public async Task Basliktaki_arama_kutusu_get_formudur()
    {
        var html = await GetAsync("/");

        Assert.Contains("action=\"/ara\"", html);
        Assert.DoesNotContain("id=\"site-search\" type=\"search\" readonly", html);
    }

    private static int CardCount(string html)
        => System.Text.RegularExpressions.Regex.Matches(html, "<article class=\"card\"").Count;

    private async Task<string> GetAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task SetDescriptionAsync(HerYerdeContext context, string slug, string description)
    {
        var dal = new EfProductDal(context);
        var product = (await dal.GetTrackedAsync(p => p.Slug == slug))!;
        product.Description = description;
        await new EfUnitOfWork(context).SaveChangesAsync();
    }
}
