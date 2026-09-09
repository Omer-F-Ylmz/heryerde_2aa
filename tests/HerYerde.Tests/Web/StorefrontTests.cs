using System.Net;
using System.Text.RegularExpressions;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Web;

[Collection(DatabaseCollection.Name)]
public sealed class StorefrontTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

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

    private static async Task<string> GetHtmlAsync(HttpClient client, string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Ana_sayfa_hero_da_gecmis_kampanya_yoktur_countdown_verisi_vardir()
    {
        var html = await GetHtmlAsync(_factory.CreateClient(), "/");

        Assert.Contains("data-countdown=\"", html);
        Assert.Contains(DataSeeder.HeroCampaignName, html);
        Assert.DoesNotContain(DataSeeder.ExpiredCampaignName + "</h1>", html);

        // geçmiş kampanyalı ürünün kartında rozet ve üstü çizili fiyat yok
        var category = await GetHtmlAsync(_factory.CreateClient(), "/ev/kucuk-ev-aletleri");
        var expiredCard = CardOf(category, DataSeeder.ExpiredCampaignName);
        Assert.DoesNotContain("tag--campaign", expiredCard);
        Assert.DoesNotContain("price-block__was", expiredCard);
    }

    [Fact]
    public async Task Ana_sayfa_ortu_kapisi_link_degil_ve_testimonials_json_okunur()
    {
        var html = await GetHtmlAsync(_factory.CreateClient(), "/");

        Assert.DoesNotContain("href=\"/ortu", html);
        Assert.DoesNotContain("href=\"/giyim", html);
        Assert.Matches("class=\"door door--soon\"[^>]*aria-disabled=\"true\"", html);

        var quotes = TestimonialSourceQuotes();
        Assert.Equal(4, quotes.Count);
        var decoded = WebUtility.HtmlDecode(html);
        foreach (var quote in quotes)
        {
            Assert.Contains(quote, decoded);
        }
    }

    [Fact]
    public async Task Ana_sayfa_yeni_gelenlerde_sekiz_kart_vardir()
    {
        var html = await GetHtmlAsync(_factory.CreateClient(), "/");
        var section = html[html.IndexOf("id=\"yeni\"", StringComparison.Ordinal)..html.IndexOf("id=\"dm\"", StringComparison.Ordinal)];

        Assert.Equal(8, Regex.Matches(section, "<article class=\"card\"").Count);
    }

    [Fact]
    public async Task Slug_sayfasi_200_pasif_ve_silinmis_urun_404_markali()
    {
        var client = _factory.CreateClient();
        await using var context = TestDb.NewContext();
        var dal = new EfProductDal(context);
        var live = await dal.GetAsync(p => p.Name == DataSeeder.HeroCampaignName);
        Assert.NotNull(live);

        var html = await GetHtmlAsync(client, "/urun/" + live.Slug);
        Assert.Contains("<h1", html);
        Assert.Contains(live.Name, html);

        var inactive = await dal.GetTrackedAsync(p => p.Name == DataSeeder.ExpiredCampaignName);
        inactive!.IsActive = false;
        var deleted = await dal.GetTrackedAsync(p => p.Slug == "hasir-piknik-sepeti");
        deleted!.DeletedAt = DateTime.UtcNow;
        await new EfUnitOfWork(context).SaveChangesAsync();

        var inactiveHtml = await GetHtmlAsync(client, "/urun/" + inactive.Slug, HttpStatusCode.NotFound);
        Assert.Contains("Bu raf boş kalmış.", inactiveHtml);
        await GetHtmlAsync(client, "/urun/hasir-piknik-sepeti", HttpStatusCode.NotFound);
        var missingHtml = await GetHtmlAsync(client, "/olmayan-sayfa", HttpStatusCode.NotFound);
        Assert.Contains("Bu raf boş kalmış.", missingHtml);
        Assert.Contains("HerYerde", missingHtml);
    }

    [Fact]
    public async Task Ev_urunu_set_icerigi_gosterir_secici_yok_whatsapp_linki_encoded()
    {
        var html = await GetHtmlAsync(_factory.CreateClient(), "/urun/granit-dokum-tencere-seti");

        Assert.Contains("Set içeriği", html);
        Assert.DoesNotContain("<fieldset", html);
        Assert.Contains("https://wa.me/905424970982?text=", html);
        Assert.Contains("Granit%20d%C3%B6k%C3%BCm%20tencere%20seti", html);
        Assert.Matches("<button[^>]*disabled[^>]*title=\"[^\"]*[Yy]akında", html);
    }

    [Fact]
    public async Task Giyim_urunu_varyant_secici_gosterir_stok_sifir_cip_disabled()
    {
        await using var context = TestDb.NewContext();
        var giyim = await new EfCategoryDal(context).GetAsync(c => c.Slug == "giyim");
        Assert.NotNull(giyim);
        var salvar = new Category { Name = "Şalvar", Slug = "salvar", ParentId = giyim.Id, SortOrder = 1, IsActive = true };
        await new EfCategoryDal(context).AddAsync(salvar);
        await new EfUnitOfWork(context).SaveChangesAsync();
        var productId = await TestData.AddProductAsync(context, salvar.Id, "Keten şalvar", "keten-salvar");
        var variantDal = new EfProductVariantDal(context);
        await variantDal.AddAsync(new ProductVariant { ProductId = productId, Size = "S", Color = "Kiremit", Sku = "KS-S-K", Stock = 3 });
        await variantDal.AddAsync(new ProductVariant { ProductId = productId, Size = "M", Color = "Kiremit", Sku = "KS-M-K", Stock = 0 });
        await new EfUnitOfWork(context).SaveChangesAsync();

        var html = await GetHtmlAsync(_factory.CreateClient(), "/urun/keten-salvar");

        Assert.Contains("<fieldset", html);
        Assert.DoesNotContain("Set içeriği", html);
        Assert.Matches("<input[^>]*id=\"size-m\"[^>]*disabled", html);
        Assert.DoesNotMatch("<input[^>]*id=\"size-s\"[^>]*disabled", html);
    }

    [Fact]
    public async Task Benzer_urunler_kendisini_icermez_ve_ayni_alt_kategoriden_gelir()
    {
        var html = await GetHtmlAsync(_factory.CreateClient(), "/urun/granit-dokum-tencere-seti");
        var section = html[html.IndexOf("id=\"benzer\"", StringComparison.Ordinal)..];

        Assert.DoesNotContain("href=\"/urun/granit-dokum-tencere-seti\"", section);
        Assert.Contains("href=\"/urun/tac-sera-feel-3lu-sahan-seti\"", section);
        Assert.DoesNotContain("href=\"/urun/hasir-piknik-sepeti\"", section);
    }

    [Fact]
    public async Task Kategori_sayfasi_24_luk_sayfalanir()
    {
        await SeedTopluAsync();
        var client = _factory.CreateClient();

        var page1 = await GetHtmlAsync(client, "/ev/toplu");
        Assert.Equal(24, Regex.Matches(page1, "<article class=\"card\"").Count);
        Assert.Contains("rel=\"next\"", page1);
        Assert.Contains("aria-current=\"page\"", page1);

        var page2 = await GetHtmlAsync(client, "/ev/toplu?sayfa=2");
        Assert.Equal(6, Regex.Matches(page2, "<article class=\"card\"").Count);
        Assert.Contains("rel=\"prev\"", page2);

        var all = await GetHtmlAsync(client, "/ev");
        Assert.Contains("href=\"/ev/toplu\"", all);
        await GetHtmlAsync(client, "/ev/olmayan-alt", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Kategori_sayfasi_fiyata_gore_artan_ve_yeniye_gore_azalan_siralanir()
    {
        await SeedTopluAsync();
        var client = _factory.CreateClient();

        var byPrice = await GetHtmlAsync(client, "/ev/toplu?sirala=fiyat");
        var cheapest = byPrice.IndexOf("Toplu ürün 30<", StringComparison.Ordinal);
        var second = byPrice.IndexOf("Toplu ürün 29<", StringComparison.Ordinal);
        Assert.True(cheapest >= 0 && second > cheapest, "fiyat sıralaması artan değil");
        Assert.DoesNotContain("Toplu ürün 1<", byPrice);
        Assert.Matches("<a class=\"tab\"[^>]*sirala=fiyat[^>]*aria-current=\"true\"", byPrice);

        var byNew = await GetHtmlAsync(client, "/ev/toplu");
        var newest = byNew.IndexOf("Toplu ürün 30<", StringComparison.Ordinal);
        var older = byNew.IndexOf("Toplu ürün 29<", StringComparison.Ordinal);
        Assert.True(newest >= 0 && older > newest, "yeni sıralaması azalan değil");
    }

    /// <summary>Ev altına "Toplu" alt kategorisi ve 30 ürün: fiyat 1000-i, i büyüdükçe daha yeni.</summary>
    private static async Task SeedTopluAsync()
    {
        await using var context = TestDb.NewContext();
        var ev = await new EfCategoryDal(context).GetAsync(c => c.Slug == "ev");
        Assert.NotNull(ev);
        var toplu = new Category { Name = "Toplu", Slug = "toplu", ParentId = ev.Id, SortOrder = 9, IsActive = true };
        await new EfCategoryDal(context).AddAsync(toplu);
        await new EfUnitOfWork(context).SaveChangesAsync();
        var productDal = new EfProductDal(context);
        for (var i = 1; i <= 30; i++)
        {
            var product = TestData.NewProduct(toplu.Id, "Toplu ürün " + i, "toplu-urun-" + i);
            product.Price = 1000 - i;
            product.CreatedAt = DateTime.UtcNow.AddMinutes(i);
            await productDal.AddAsync(product);
        }

        await new EfUnitOfWork(context).SaveChangesAsync();
    }

    private static string CardOf(string html, string productName)
    {
        var at = html.IndexOf(productName, StringComparison.Ordinal);
        Assert.True(at >= 0, productName + " kartı bulunamadı.");
        var start = html.LastIndexOf("<article class=\"card\"", at, StringComparison.Ordinal);
        var end = html.IndexOf("</article>", at, StringComparison.Ordinal);
        return html[start..end];
    }

    private static List<string> TestimonialSourceQuotes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "testimonials.json");
        Assert.True(File.Exists(path), "testimonials.json çıktı dizinine kopyalanmamış.");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateArray().Select(e => e.GetProperty("quote").GetString()!).ToList();
    }
}
