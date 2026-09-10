using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Web;

/// <summary>Sayfa başına sorgu sayısı: N+1 ve "hepsini belleğe al" geri gelirse kırmızı yanar.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PerfQueryTests : IAsyncLifetime
{
    private readonly CountingFactory _factory = new();

    public async Task InitializeAsync()
    {
        await TestDb.ResetAsync();
        await using var context = TestDb.NewContext();
        await DataSeeder.SeedCatalogAsync(context);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Ana_sayfa_en_cok_uc_sorgu_atar()
        => Assert.InRange(await _factory.QueryCountAsync(_factory.CreateClient(), "/"), 1, 3);

    [Fact]
    public async Task Ev_listesi_en_cok_uc_sorgu_atar()
        => Assert.InRange(await _factory.QueryCountAsync(_factory.CreateClient(), "/ev"), 1, 3);

    [Fact]
    public async Task Alt_kategori_en_cok_uc_sorgu_atar()
        => Assert.InRange(await _factory.QueryCountAsync(_factory.CreateClient(), "/ev/tencere-tava"), 1, 3);

    [Fact]
    public async Task Urun_sayfasi_en_cok_bes_sorgu_atar()
        => Assert.InRange(await _factory.QueryCountAsync(_factory.CreateClient(), "/urun/granit-dokum-tencere-seti"), 1, 5);

    /// <summary>Üç liste sorgusu + G12 ile gelen yönetici parola damgası kontrolü.</summary>
    [Fact]
    public async Task Yonetim_urun_listesi_en_cok_dort_sorgu_atar()
    {
        var client = await _factory.CreateSignedInClientAsync();
        Assert.InRange(await _factory.QueryCountAsync(client, "/admin/products"), 1, 4);
    }

    [Fact]
    public async Task Yonetim_urun_listesi_stok_toplamini_tek_gruplu_sorguda_alir()
    {
        var client = await _factory.CreateSignedInClientAsync();
        await _factory.QueryCountAsync(client, "/admin/products");

        Assert.Contains(_factory.Counter.Commands, c => c.Contains("GROUP BY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sepet_sayfasi_en_cok_dort_sorgu_atar()
    {
        var client = _factory.CreateNonRedirectingClient();
        await AddHomeProductToCartAsync(client, "granit-dokum-tencere-seti");
        await AddHomeProductToCartAsync(client, "dokum-tava-28-cm");

        Assert.InRange(await _factory.QueryCountAsync(client, "/sepet"), 1, 4);
    }

    [Fact]
    public async Task Cerezsiz_istekte_sepet_rozeti_hic_sorgu_atmaz()
    {
        await _factory.QueryCountAsync(_factory.CreateClient(), "/");

        Assert.DoesNotContain(_factory.Counter.Commands, c => c.Contains("cart_item", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ev_listesi_sayfalamayi_veritabaninda_yapar()
    {
        await using (var context = TestDb.NewContext())
        {
            var categoryId = context.Categories.First(c => c.Slug == "saklama-duzenleme").Id;
            for (var i = 0; i < 12; i++)
            {
                context.Products.Add(TestData.NewProduct(categoryId, $"Dolgu ürün {i}", $"dolgu-urun-{i}"));
            }

            await context.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        _factory.Counter.Reset();
        var response = await client.GetAsync("/ev?sayfa=2");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(html, "<article class=\"card\"").Count);
        Assert.Contains(_factory.Counter.Commands, c => c.Contains("OFFSET", StringComparison.Ordinal) && c.Contains("FETCH NEXT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ev_listesi_fiyat_siralamasini_veritabaninda_yapar()
    {
        await _factory.QueryCountAsync(_factory.CreateClient(), "/ev?sirala=fiyat");

        Assert.Contains(
            _factory.Counter.Commands,
            c => c.Contains("ORDER BY", StringComparison.Ordinal) && c.Contains("campaign_price", StringComparison.Ordinal));
    }

    private async Task AddHomeProductToCartAsync(HttpClient client, string slug)
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = context.Products.First(p => p.Slug == slug).Id;
        }

        var response = await HtmlForm.PostAsync(client, "/urun/" + slug, "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
