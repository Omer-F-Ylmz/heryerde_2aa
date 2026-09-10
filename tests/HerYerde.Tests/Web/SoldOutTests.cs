using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>G08 vitrin tarafı: tükendi rozeti kartta ve detayda çıkar, sepete ekle pasifleşir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SoldOutTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Stogu_biten_ev_urunu_kartta_tukendi_rozeti_tasir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 0);
        await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", stock: 3);

        var html = await GetAsync("/ev");

        Assert.Contains("tag--soldout", html);
        Assert.Contains("Tükendi", html);
        Assert.Equal(1, Occurrences(html, "tag--soldout"));
    }

    [Fact]
    public async Task Tukenen_urunun_detayinda_sepete_ekle_pasiftir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 0);

        var html = await GetAsync("/urun/celik-tencere");

        Assert.Contains("tag--soldout", html);
        Assert.Matches("data-add-button[^>]*disabled|disabled[^>]*data-add-button", html);
        Assert.Contains("şimdilik tükendi", html);
    }

    [Fact]
    public async Task Stok_tutmayan_ev_urunu_tukendi_sayilmaz()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet");

        var html = await GetAsync("/urun/hasir-sepet");

        Assert.DoesNotContain("tag--soldout", html);
    }

    [Fact]
    public async Task Tum_varyantlari_biten_giyim_urunu_tukendi_gorunur()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 0);

        var html = await GetAsync("/urun/salvar");

        Assert.Contains("tag--soldout", html);
    }

    private static int Occurrences(string html, string needle)
        => System.Text.RegularExpressions.Regex.Matches(html, System.Text.RegularExpressions.Regex.Escape(needle)).Count;

    private async Task<string> GetAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
