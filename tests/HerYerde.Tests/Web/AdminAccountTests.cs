using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business.Rules;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Areas.Admin.Controllers;
using HerYerde.Web.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace HerYerde.Tests.Web;

/// <summary>D11 B2: parola sıfırlama bağlantısı (30 dk, tek kullanım), CLI geçici parola (ilk girişte değiştirme zorunlu),
/// TOTP iki adımlı doğrulama (kurulum, 5 hatada kilit, tek kullanımlık yedek kod, kapatma parola ister), tüm oturumları kapat.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminAccountTests : IAsyncLifetime
{
    private const string NewPassword = "Yeni-Parola-2026";

    private readonly MovableClockFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Sifirlama_baglantisi_parolayi_degistirir_ve_tek_kullanimliktir()
    {
        var anonymous = _factory.CreateNonRedirectingClient();
        var link = await RequestResetLinkAsync(anonymous);

        var first = await ResetAsync(anonymous, link);
        var reused = await ResetAsync(anonymous, link, "Baska-Parola-2027");

        Assert.Equal(HttpStatusCode.Found, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await LoginAsync(_factory.CreateNonRedirectingClient(), NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Suresi_dolmus_sifirlama_baglantisi_400()
    {
        var anonymous = _factory.CreateNonRedirectingClient();
        var link = await RequestResetLinkAsync(anonymous);
        _factory.Clock.Advance(TimeSpan.FromMinutes(31));

        var response = await ResetAsync(anonymous, link);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await LoginAsync(_factory.CreateNonRedirectingClient(), AdminWebFactory.AdminPassword)).StatusCode);
    }

    [Fact]
    public async Task Bilinmeyen_eposta_ayni_yaniti_alir_posta_gitmez()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await HtmlForm.PostAsync(client, "/admin/auth/sifremi-unuttum", "/admin/auth/sifremi-unuttum",
            new Dictionary<string, string> { ["Email"] = "yok@heryerde.test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("bağlantı gönderildi", await response.Content.ReadAsStringAsync());
        await using var context = TestDb.NewContext();
        Assert.Empty(await new EfOutboxMessageDal(context).GetListAsync());
    }

    /// <summary>KAPANIŞ-3 S-02: kayıtlı e-postada token + posta yazımı yapılır, kayıtsızda yapılmaz; yanıt süresi farkı hesabı ele
    /// vermesin diye iki yol da aynı asgari sürede döner.</summary>
    [Fact]
    public async Task Sifremi_unuttum_kayitli_ve_kayitsiz_epostada_ayni_asgari_surede_doner()
    {
        var client = _factory.CreateNonRedirectingClient();

        async Task<TimeSpan> ElapsedAsync(string email)
        {
            var token = await HtmlForm.AntiforgeryTokenAsync(client, "/admin/auth/sifremi-unuttum");
            var started = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.PostAsync("/admin/auth/sifremi-unuttum", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["__RequestVerificationToken"] = token
            }));
            started.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return started.Elapsed;
        }

        var known = await ElapsedAsync(AdminWebFactory.AdminEmail);
        var unknown = await ElapsedAsync("yok@heryerde.test");

        Assert.True(known >= AuthController.ResetResponseFloor - TimeSpan.FromMilliseconds(20), $"kayıtlı {known.TotalMilliseconds} ms");
        Assert.True(unknown >= AuthController.ResetResponseFloor - TimeSpan.FromMilliseconds(20), $"kayıtsız {unknown.TotalMilliseconds} ms");
    }

    [Fact]
    public async Task Cli_sifirlamasi_gecici_parolayla_girisi_parola_degistirmeye_zorlar()
    {
        _ = _factory.Services;
        await using (var context = TestDb.NewContext())
        {
            Assert.NotEmpty(await new EfAdminUserDal(context).GetListAsync());
        }

        var temporary = await AdminResetCommand.RunAsync(_factory.Services, AdminWebFactory.AdminEmail);
        var client = _factory.CreateNonRedirectingClient();

        var login = await LoginAsync(client, temporary);
        var products = await client.GetAsync("/admin/products");

        Assert.Equal("/admin/sifre", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Found, products.StatusCode);
        Assert.Equal("/admin/sifre", products.Headers.Location!.AbsolutePath);

        var changed = await HtmlForm.PostAsync(client, "/admin/sifre", "/admin/sifre", new Dictionary<string, string>
        {
            ["CurrentPassword"] = temporary,
            ["NewPassword"] = NewPassword,
            ["ConfirmPassword"] = NewPassword
        });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Iki_adimli_dogrulama_kurulur_ve_giriste_kod_ister()
    {
        var admin = await _factory.CreateSignedInClientAsync();
        var (secret, _) = await EnableTotpAsync(admin);

        var client = _factory.CreateNonRedirectingClient();
        var login = await LoginAsync(client, AdminWebFactory.AdminPassword);
        var blocked = await client.GetAsync("/admin/products");
        var verified = await HtmlForm.PostAsync(client, "/admin/auth/iki-adim", "/admin/auth/iki-adim",
            new Dictionary<string, string> { ["Code"] = Totp.Code(secret, _factory.Clock.GetUtcNow()) });

        Assert.Equal("/admin/auth/iki-adim", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Found, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Found, verified.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Bes_yanlis_kod_hesabi_kilitler_dogru_kod_da_gecmez()
    {
        var admin = await _factory.CreateSignedInClientAsync();
        var (secret, _) = await EnableTotpAsync(admin);
        var client = _factory.CreateNonRedirectingClient();
        await LoginAsync(client, AdminWebFactory.AdminPassword);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var wrong = await HtmlForm.PostAsync(client, "/admin/auth/iki-adim", "/admin/auth/iki-adim",
                new Dictionary<string, string> { ["Code"] = "000000" });
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        var locked = await HtmlForm.PostAsync(client, "/admin/auth/iki-adim", "/admin/auth/iki-adim",
            new Dictionary<string, string> { ["Code"] = Totp.Code(secret, _factory.Clock.GetUtcNow()) });

        Assert.NotEqual(HttpStatusCode.Found, locked.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Yedek_kod_bir_kez_kullanilir()
    {
        var admin = await _factory.CreateSignedInClientAsync();
        var (_, recoveryCodes) = await EnableTotpAsync(admin);
        Assert.Equal(8, recoveryCodes.Count);

        var first = _factory.CreateNonRedirectingClient();
        await LoginAsync(first, AdminWebFactory.AdminPassword);
        var used = await HtmlForm.PostAsync(first, "/admin/auth/iki-adim", "/admin/auth/iki-adim",
            new Dictionary<string, string> { ["Code"] = recoveryCodes[0] });

        var second = _factory.CreateNonRedirectingClient();
        await LoginAsync(second, AdminWebFactory.AdminPassword);
        var reused = await HtmlForm.PostAsync(second, "/admin/auth/iki-adim", "/admin/auth/iki-adim",
            new Dictionary<string, string> { ["Code"] = recoveryCodes[0] });

        Assert.Equal(HttpStatusCode.Found, used.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    [Fact]
    public async Task Kullanilmis_totp_kodu_ayni_pencerede_ikinci_giriste_gecmez()
    {
        var admin = await _factory.CreateSignedInClientAsync();
        var (secret, _) = await EnableTotpAsync(admin);
        var code = Totp.Code(secret, _factory.Clock.GetUtcNow());

        var first = _factory.CreateNonRedirectingClient();
        await LoginAsync(first, AdminWebFactory.AdminPassword);
        var used = await HtmlForm.PostAsync(first, "/admin/auth/iki-adim", "/admin/auth/iki-adim",
            new Dictionary<string, string> { ["Code"] = code });

        var second = _factory.CreateNonRedirectingClient();
        await LoginAsync(second, AdminWebFactory.AdminPassword);
        var replayed = await HtmlForm.PostAsync(second, "/admin/auth/iki-adim", "/admin/auth/iki-adim",
            new Dictionary<string, string> { ["Code"] = code });

        Assert.Equal(HttpStatusCode.Found, used.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, replayed.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await second.GetAsync("/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Iki_adimli_dogrulamayi_kapatmak_parola_ister()
    {
        var admin = await _factory.CreateSignedInClientAsync();
        await EnableTotpAsync(admin);

        var wrong = await HtmlForm.PostAsync(admin, "/admin/iki-adim", "/admin/iki-adim/kapat",
            new Dictionary<string, string> { ["Password"] = "yanlis-parola-1" });
        var stillOn = await EnabledAsync();
        var right = await HtmlForm.PostAsync(admin, "/admin/iki-adim", "/admin/iki-adim/kapat",
            new Dictionary<string, string> { ["Password"] = AdminWebFactory.AdminPassword });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.True(stillOn);
        Assert.Equal(HttpStatusCode.Found, right.StatusCode);
        Assert.False(await EnabledAsync());
    }

    [Fact]
    public async Task Tum_oturumlari_kapatinca_diger_tarayicinin_cerezi_girise_duser()
    {
        var mine = await _factory.CreateSignedInClientAsync();
        var other = await _factory.CreateSignedInClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/admin/products")).StatusCode);

        var revoked = await HtmlForm.PostAsync(mine, "/admin/sifre", "/admin/oturumlar/kapat", new());

        Assert.Equal(HttpStatusCode.Found, revoked.StatusCode);
        var stale = await other.GetAsync("/admin/products");
        Assert.Equal(HttpStatusCode.Found, stale.StatusCode);
        Assert.Contains("/admin/auth/login", stale.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await mine.GetAsync("/admin/products")).StatusCode);
    }

    private async Task<(string Secret, List<string> RecoveryCodes)> EnableTotpAsync(HttpClient admin)
    {
        var setup = await (await admin.GetAsync("/admin/iki-adim")).Content.ReadAsStringAsync();
        var secret = Regex.Match(setup, "data-totp-secret=\"([A-Z2-7]+)\"").Groups[1].Value;
        Assert.NotEmpty(secret);

        var enabled = await HtmlForm.PostAsync(admin, "/admin/iki-adim", "/admin/iki-adim",
            new Dictionary<string, string> { ["Code"] = Totp.Code(secret, _factory.Clock.GetUtcNow()) });
        var html = await enabled.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.True(await EnabledAsync());
        return (secret, Regex.Matches(html, "data-recovery-code>([^<]+)<").Select(m => m.Groups[1].Value).ToList());
    }

    private static async Task<bool> EnabledAsync()
    {
        await using var context = TestDb.NewContext();
        return Assert.Single(await new EfAdminUserDal(context).GetListAsync()).TotpEnabled;
    }

    private static async Task<string> RequestResetLinkAsync(HttpClient client)
    {
        var response = await HtmlForm.PostAsync(client, "/admin/auth/sifremi-unuttum", "/admin/auth/sifremi-unuttum",
            new Dictionary<string, string> { ["Email"] = AdminWebFactory.AdminEmail });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = TestDb.NewContext();
        var mail = Assert.Single(await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.AdminPasswordReset));
        Assert.Equal(AdminWebFactory.AdminEmail, mail.To);
        var match = Regex.Match(mail.Body, "/admin/auth/sifre-sifirla\\?t=([A-Za-z0-9_-]+)");
        Assert.True(match.Success);
        return "/admin/auth/sifre-sifirla?t=" + match.Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> ResetAsync(HttpClient client, string link, string password = NewPassword)
    {
        var token = link[(link.IndexOf("?t=", StringComparison.Ordinal) + 3)..];
        var form = await client.GetAsync(link);
        var html = await form.Content.ReadAsStringAsync();
        var antiforgery = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!antiforgery.Success)
        {
            antiforgery = Regex.Match(await (await client.GetAsync("/admin/auth/login")).Content.ReadAsStringAsync(),
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        }

        return await client.PostAsync("/admin/auth/sifre-sifirla", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Token"] = token,
            ["NewPassword"] = password,
            ["ConfirmPassword"] = password,
            ["__RequestVerificationToken"] = antiforgery.Groups[1].Value
        }));
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string password)
        => HtmlForm.PostAsync(client, "/admin/auth/login", "/admin/auth/login", new Dictionary<string, string>
        {
            ["Email"] = AdminWebFactory.AdminEmail,
            ["Password"] = password
        });

    private sealed class MovableClockFactory : AdminWebFactory
    {
        public TestClock.MovableTimeProvider Clock { get; } = TestClock.Movable();

        protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton<TimeProvider>(Clock);
    }
}
