using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>G11: min/max fiyat aralığı SQL'de süzülür; ters aralık sessizce takas edilir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PriceFilterTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public async Task InitializeAsync()
    {
        await TestDb.ResetAsync();
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Ucuz Set", "ucuz-set", price: 100m);
        await TestData.AddHomeProductAsync(context, "Orta Set", "orta-set", price: 500m);
        await TestData.AddHomeProductAsync(context, "Pahalı Set", "pahali-set", price: 2000m);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Aralik_disindaki_urunler_listeye_girmez()
    {
        var html = await GetAsync("/ara?q=set&min=200&max=1000");

        Assert.Contains("Orta Set", html);
        Assert.DoesNotContain("Ucuz Set", html);
        Assert.DoesNotContain("Pahalı Set", html);
    }

    [Fact]
    public async Task Ters_aralik_sessizce_takas_edilir()
    {
        var swapped = await GetAsync("/ara?q=set&min=1000&max=200");
        var straight = await GetAsync("/ara?q=set&min=200&max=1000");

        Assert.Contains("Orta Set", swapped);
        Assert.DoesNotContain("Pahalı Set", swapped);
        Assert.Equal(CardCount(straight), CardCount(swapped));
    }

    [Fact]
    public async Task Kampanya_fiyati_aralikta_esas_alinir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Kampanyalı Set", "kampanyali-set", price: 3000m, campaignPrice: 300m);

        var html = await GetAsync("/ara?q=set&min=200&max=1000");

        Assert.Contains("Kampanyalı Set", html);
    }

    [Fact]
    public async Task Siralama_ve_sayfa_baglantilari_araligi_korur()
    {
        var html = await GetAsync("/ara?q=set&min=200&max=1000");

        Assert.Contains("sirala=fiyat&amp;min=200&amp;max=1000", html);
        Assert.Contains("name=\"min\"", html);
        Assert.Contains("value=\"200\"", html);
    }

    [Fact]
    public async Task Kategori_sayfasinda_da_aralik_calisir()
    {
        var html = await GetAsync("/ev?min=200&max=1000");

        Assert.Contains("Orta Set", html);
        Assert.DoesNotContain("Pahalı Set", html);
    }

    private static int CardCount(string html)
        => System.Text.RegularExpressions.Regex.Matches(html, "<article class=\"card\"").Count;

    private async Task<string> GetAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
