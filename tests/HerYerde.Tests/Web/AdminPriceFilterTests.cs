using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>D5-B4: ithalden gelen fiyatsız ürünler yönetici listesinde süzülebilir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminPriceFilterTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Fiyat_eksik_suzgeci_yalniz_fiyatsiz_urunleri_listeler()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", price: 1m);
            await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", price: 349m);
        }

        var client = await _factory.CreateSignedInClientAsync();

        var all = await client.GetAsync("/admin/products");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        var allHtml = await all.Content.ReadAsStringAsync();
        Assert.Contains("Hasır Sepet", allHtml, StringComparison.Ordinal);
        Assert.Contains("Cam Sürahi", allHtml, StringComparison.Ordinal);

        var filtered = await client.GetAsync("/admin/products?fiyat=eksik");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var filteredHtml = await filtered.Content.ReadAsStringAsync();

        Assert.Contains("Hasır Sepet", filteredHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Cam Sürahi", filteredHtml, StringComparison.Ordinal);
    }
}
