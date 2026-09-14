using System.Net;
using System.Text.RegularExpressions;

namespace HerYerde.Tests.Web;

/// <summary>D5-A2: yüklenen görsel vitrinde srcset ile üç boyutta sunulur; yer tutucu yalnız görselsiz üründe kalır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductImageMarkupTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private const string Uploaded = "/uploads/products/1/0123456789abcdef0123456789abcdef-800.webp";

    private async Task<string> GetHtmlAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Kartta_srcset_uc_boyutu_ve_sizes_icerir()
    {
        await using (var context = TestDb.NewContext())
        {
            var categoryId = await TestData.AddChildCategoryAsync(context, "Sepet & Dekor", "sepet-dekor");
            var productId = await TestData.AddProductAsync(context, categoryId, "Hasır Sepet", "hasir-sepet");
            await TestData.AddImageAsync(context, productId, Uploaded);
        }

        var html = await GetHtmlAsync("/ev/sepet-dekor");
        var card = CardOf(html, "Hasır Sepet");

        Assert.Contains("-400.webp 400w", card, StringComparison.Ordinal);
        Assert.Contains("-800.webp 800w", card, StringComparison.Ordinal);
        Assert.Contains("-1200.webp 1200w", card, StringComparison.Ordinal);
        Assert.Contains("sizes=\"", card, StringComparison.Ordinal);
        // İlk dört kart katlamanın üstünde sayılır; tek ürünlü listede kart eager yüklenir.
        Assert.Contains("loading=\"eager\"", card, StringComparison.Ordinal);
        Assert.Contains("width=\"600\" height=\"600\"", card, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gorselsiz_urunde_yer_tutucu_gorunur()
    {
        await using (var context = TestDb.NewContext())
        {
            var categoryId = await TestData.AddChildCategoryAsync(context, "Sepet & Dekor", "sepet-dekor");
            await TestData.AddProductAsync(context, categoryId, "Rafya Sepet", "rafya-sepet");
        }

        var html = await GetHtmlAsync("/urun/rafya-sepet");

        Assert.Contains("class=\"ph ph--basket\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=\"/uploads/", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gorselli_urun_sayfasinda_yer_tutucu_yerine_srcsetli_img_vardir()
    {
        await using (var context = TestDb.NewContext())
        {
            var categoryId = await TestData.AddChildCategoryAsync(context, "Sepet & Dekor", "sepet-dekor");
            var productId = await TestData.AddProductAsync(context, categoryId, "Hasır Sepet", "hasir-sepet");
            await TestData.AddImageAsync(context, productId, Uploaded);
        }

        var html = await GetHtmlAsync("/urun/hasir-sepet");

        Assert.DoesNotContain("class=\"ph", html, StringComparison.Ordinal);
        Assert.Contains(Uploaded, html, StringComparison.Ordinal);
        Assert.Contains("-1200.webp 1200w", html, StringComparison.Ordinal);
    }

    private static string CardOf(string html, string productName)
    {
        var cards = Regex.Matches(html, "<article class=\"card\".*?</article>", RegexOptions.Singleline);
        var card = cards.FirstOrDefault(m => m.Value.Contains(productName, StringComparison.Ordinal));
        Assert.NotNull(card);
        return card.Value;
    }
}
