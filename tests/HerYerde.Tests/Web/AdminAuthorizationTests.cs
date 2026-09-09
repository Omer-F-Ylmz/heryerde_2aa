using System.Net;

namespace HerYerde.Tests.Web;

[Collection(DatabaseCollection.Name)]
public sealed class AdminAuthorizationTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/products")]
    [InlineData("/admin/categories")]
    [InlineData("/admin/orders")]
    public async Task Yetkisiz_ziyaretci_giris_sayfasina_yonlendirilir(string url)
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location!;
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];
        Assert.Equal("/admin/auth/login", path);
    }

    [Fact]
    public async Task Giris_sayfasi_oturumsuz_acilir()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync("/admin/auth/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ciplak_admin_adresi_giris_sonrasi_urun_listesine_yonlenir()
    {
        var client = await _factory.CreateSignedInClientAsync();

        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/admin/products", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Hatali_parola_giris_sayfasinda_kalir_ve_cerez_vermez()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await HtmlForm.PostAsync(client, "/admin/auth/login", "/admin/auth/login", new Dictionary<string, string>
        {
            ["Email"] = AdminWebFactory.AdminEmail,
            ["Password"] = "yanlis-parola"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie") && response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("heryerde.admin")));
    }
}
