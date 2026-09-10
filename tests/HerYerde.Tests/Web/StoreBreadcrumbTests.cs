using System.Net;
using System.Text.RegularExpressions;

namespace HerYerde.Tests.Web;

/// <summary>B08: kırıntının kökü ürünün kök alanından gelir; giyim ürününde "Ev ›" ve /ev/giyim yoktur.</summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class StoreBreadcrumbTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Giyim_urununde_kirinti_baglantilari_404_vermez()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddClothingProductAsync(context, "Şalvar", "salvar");
        var client = _factory.CreateNonRedirectingClient();

        var html = await (await client.GetAsync("/urun/salvar")).Content.ReadAsStringAsync();

        var links = CrumbLinks(html);
        Assert.NotEmpty(links);
        foreach (var link in links)
        {
            Assert.NotEqual(HttpStatusCode.NotFound, (await client.GetAsync(link)).StatusCode);
        }
    }

    [Fact]
    public async Task Giyim_urununun_kok_kirintisi_ev_degildir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddClothingProductAsync(context, "Şalvar", "salvar");
        var client = _factory.CreateNonRedirectingClient();

        var html = await (await client.GetAsync("/urun/salvar")).Content.ReadAsStringAsync();

        var crumbs = CrumbBlock(html);
        Assert.Contains("Örtü", crumbs);
        Assert.DoesNotContain("/ev/giyim", crumbs);
    }

    [Fact]
    public async Task Ev_urununde_kok_kirinti_ev_kategorisine_gider()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = _factory.CreateNonRedirectingClient();

        var html = await (await client.GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();

        Assert.Contains("href=\"/ev\"", CrumbBlock(html));
    }

    private static string CrumbBlock(string html)
    {
        var match = CrumbPattern().Match(html);
        Assert.True(match.Success, "Ürün sayfasında kırıntı bulunamadı.");
        return match.Value;
    }

    private static List<string> CrumbLinks(string html)
        => LinkPattern().Matches(CrumbBlock(html)).Select(m => m.Groups[1].Value).ToList();

    [GeneratedRegex("<nav class=\"crumbs\".*?</nav>", RegexOptions.Singleline)]
    private static partial Regex CrumbPattern();

    [GeneratedRegex("href=\"([^\"]+)\"")]
    private static partial Regex LinkPattern();
}
