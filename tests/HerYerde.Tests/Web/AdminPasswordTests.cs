using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>G12: /admin/sifre — kural dışı parola 400, başarıda diğer oturumlar düşer.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminPasswordTests : IAsyncLifetime
{
    private const string NewPassword = "YeniParola9x";

    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("kisa1")]
    [InlineData("rakamsizparola")]
    public async Task Kural_disi_yeni_parola_400_doner(string password)
    {
        var client = await _factory.CreateSignedInClientAsync();

        var response = await PostAsync(client, AdminWebFactory.AdminPassword, password, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Yanlis_mevcut_parola_400_doner()
    {
        var client = await _factory.CreateSignedInClientAsync();

        var response = await PostAsync(client, "YanlisParola1", NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Mevcut parola hatalı.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Tekrar_uyusmazsa_400_doner()
    {
        var client = await _factory.CreateSignedInClientAsync();

        var response = await PostAsync(client, AdminWebFactory.AdminPassword, NewPassword, NewPassword + "z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Parola_degisince_eski_cerezli_oturum_giris_sayfasina_dusdurulur()
    {
        var stale = await _factory.CreateSignedInClientAsync();
        var current = await _factory.CreateSignedInClientAsync();

        var changed = await PostAsync(current, AdminWebFactory.AdminPassword, NewPassword, NewPassword);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var staleResponse = await stale.GetAsync("/admin/products");
        Assert.Equal(HttpStatusCode.Found, staleResponse.StatusCode);
        Assert.Contains("/admin/auth/login", staleResponse.Headers.Location!.ToString());

        // Parolayı değiştiren tarayıcı yeni damgayla imzalandığı için açık kalır.
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Yeni_parola_ile_giris_yapilir_eskisiyle_yapilmaz()
    {
        var client = await _factory.CreateSignedInClientAsync();
        await PostAsync(client, AdminWebFactory.AdminPassword, NewPassword, NewPassword);

        var fresh = _factory.CreateNonRedirectingClient();
        var withNew = await HtmlForm.PostAsync(fresh, "/admin/auth/login", "/admin/auth/login", new Dictionary<string, string>
        {
            ["Email"] = AdminWebFactory.AdminEmail,
            ["Password"] = NewPassword
        });

        Assert.Equal(HttpStatusCode.Found, withNew.StatusCode);

        var other = _factory.CreateNonRedirectingClient();
        var withOld = await HtmlForm.PostAsync(other, "/admin/auth/login", "/admin/auth/login", new Dictionary<string, string>
        {
            ["Email"] = AdminWebFactory.AdminEmail,
            ["Password"] = AdminWebFactory.AdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, withOld.StatusCode);
    }

    [Fact]
    public async Task Menude_parola_baglantisi_vardir()
    {
        var client = await _factory.CreateSignedInClientAsync();

        var html = await (await client.GetAsync("/admin/products")).Content.ReadAsStringAsync();

        Assert.Contains("/admin/sifre", html);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string current, string next, string confirm)
        => HtmlForm.PostAsync(client, "/admin/sifre", "/admin/sifre", new Dictionary<string, string>
        {
            ["CurrentPassword"] = current,
            ["NewPassword"] = next,
            ["ConfirmPassword"] = confirm
        });
}
