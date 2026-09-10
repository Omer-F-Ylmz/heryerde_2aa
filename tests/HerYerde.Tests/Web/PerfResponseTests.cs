using System.Net.Http.Headers;

namespace HerYerde.Tests.Web;

/// <summary>Yanıt sıkıştırma ve statik dosya önbelleği.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PerfResponseTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public async Task InitializeAsync() => await TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, string? acceptEncoding = null)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (acceptEncoding is not null)
        {
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(acceptEncoding));
        }

        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task Html_istenen_kodlamayla_sikistirilir(string encoding)
    {
        var response = await GetAsync("/", encoding);

        Assert.Equal(encoding, Assert.Single(response.Content.Headers.ContentEncoding));
    }

    [Fact]
    public async Task Css_sikistirilir()
    {
        var response = await GetAsync("/css/site.css", "br");

        Assert.Equal("br", Assert.Single(response.Content.Headers.ContentEncoding));
    }

    [Fact]
    public async Task Statik_dosya_bir_yil_degismez_olarak_onbelleklenir()
    {
        var response = await GetAsync("/css/site.css");

        var cacheControl = Assert.Single(response.Headers.GetValues("Cache-Control"));
        Assert.Contains("public", cacheControl, StringComparison.Ordinal);
        Assert.Contains("max-age=31536000", cacheControl, StringComparison.Ordinal);
        Assert.Contains("immutable", cacheControl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Yazi_tipi_bir_yil_degismez_olarak_onbelleklenir()
    {
        var response = await GetAsync("/fonts/figtree-latin.woff2");

        var cacheControl = Assert.Single(response.Headers.GetValues("Cache-Control"));
        Assert.Contains("max-age=31536000", cacheControl, StringComparison.Ordinal);
        Assert.Contains("immutable", cacheControl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Html_degismez_olarak_isaretlenmez()
    {
        var response = await GetAsync("/");

        var cacheControl = response.Headers.CacheControl?.ToString() ?? string.Empty;
        Assert.DoesNotContain("immutable", cacheControl, StringComparison.Ordinal);
        Assert.DoesNotContain("max-age=31536000", cacheControl, StringComparison.Ordinal);
    }
}
