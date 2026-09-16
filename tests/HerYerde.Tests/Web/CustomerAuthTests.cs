using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Controllers;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D16 B: isteğe bağlı üye hesabı — kayıt (KVKK onayı zorunlu, pazarlama izni ayrı ve kapalı), e-posta doğrulama,
/// parolalı ve parolasız (15 dk tek kullanımlık bağlantı) giriş, parola sıfırlama, hesap sayımı yok, ayrı müşteri çerezi,
/// 30 gün kayan oturum, "tüm cihazlardan çık", hız sınırları.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CustomerAuthTests : IAsyncLifetime
{
    public const string Email = "ayse@example.com";
    public const string Password = "UzunParola12";
    private const string SentMessage = "E-postanıza bir bağlantı gönderdik";
    private const string CredentialsMessage = "E-posta ya da parola hatalı.";

    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Kayit_kvkk_onayi_olmadan_400_doner_hesap_acilmaz()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await SignUpAsync(client, Email, Password, kvkk: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("aydınlatma metnini okuduğunuzu onaylayın", await response.Content.ReadAsStringAsync());
        await using var context = TestDb.NewContext();
        Assert.Empty(await context.Customers.ToListAsync());
    }

    [Fact]
    public async Task Kayit_on_karakterden_kisa_parolada_400_doner()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await SignUpAsync(client, Email, "Kisa12345");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("en az 10 karakter", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Kayit_dogrulanmamis_hesap_acar_onay_anini_ve_surumu_yazar_pazarlama_kapali_dogrulama_postasi_gider_oturum_acmaz()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await SignUpAsync(client, "Ayse@Example.com", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(SentMessage, await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("heryerde.customer=", StringComparison.Ordinal)));

        await using var context = TestDb.NewContext();
        var customer = Assert.Single(await context.Customers.AsNoTracking().ToListAsync());
        Assert.Equal(Email, customer.Email);
        Assert.NotNull(customer.PasswordHash);
        Assert.DoesNotContain(Password, customer.PasswordHash);
        Assert.Null(customer.EmailVerifiedAt);
        Assert.Equal(TestClock.Now, customer.KvkkConsentAt);
        Assert.Equal(LegalDocs.Version, customer.LegalVersion);
        Assert.False(customer.MarketingConsent);
        Assert.Null(customer.MarketingConsentAt);

        var mail = Assert.Single(await context.OutboxMessages.AsNoTracking().ToListAsync());
        Assert.Equal(OutboxType.CustomerVerify, mail.Type);
        Assert.Equal(Email, mail.To);
        Assert.Matches("/hesap/dogrula\\?t=[A-Za-z0-9_-]{40,}", mail.Body);
    }

    [Fact]
    public async Task Kayitli_epostayla_yeniden_kayit_ayni_yaniti_verir_ikinci_hesap_acmaz_parolayi_degistirmez()
    {
        await using var context = TestDb.NewContext();
        var id = await TestData.AddCustomerAsync(context, Email, Password);
        var hash = (await context.Customers.AsNoTracking().SingleAsync()).PasswordHash;
        var client = _factory.CreateNonRedirectingClient();

        var response = await SignUpAsync(client, Email, "BaskaParola99");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(SentMessage, await response.Content.ReadAsStringAsync());
        await using var check = TestDb.NewContext();
        var customer = Assert.Single(await check.Customers.AsNoTracking().ToListAsync());
        Assert.Equal(id, customer.Id);
        Assert.Equal(hash, customer.PasswordHash);
        Assert.Equal(OutboxType.CustomerLoginLink, Assert.Single(await check.OutboxMessages.AsNoTracking().ToListAsync()).Type);
    }

    [Fact]
    public async Task Dogrulama_baglantisi_GETte_durumu_degistirmez_POSTta_parolayla_dogrular_oturum_acar_ayni_epostali_misafir_siparislerini_baglar()
    {
        await using var context = TestDb.NewContext();
        var mine = await PlaceGuestOrderAsync(context, "AYSE@example.com", "tencere-a");
        var other = await PlaceGuestOrderAsync(context, "baska@example.com", "tencere-b");
        var client = _factory.CreateNonRedirectingClient();
        await SignUpAsync(client, Email, Password);
        var link = await MailLinkAsync(OutboxType.CustomerVerify, "/hesap/dogrula");

        var page = await client.GetAsync(link);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        await using (var unchanged = TestDb.NewContext())
        {
            Assert.Null((await unchanged.Customers.AsNoTracking().SingleAsync()).EmailVerifiedAt);
        }

        var wrong = await ConfirmAsync(client, link, "/hesap/dogrula", "YanlisParola1");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var confirmed = await ConfirmAsync(client, link, "/hesap/dogrula", Password);
        Assert.Equal(HttpStatusCode.Found, confirmed.StatusCode);
        Assert.Equal("/hesap", confirmed.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/hesap")).StatusCode);

        await using var check = TestDb.NewContext();
        var customer = await check.Customers.AsNoTracking().SingleAsync();
        Assert.Equal(TestClock.Now, customer.EmailVerifiedAt);
        Assert.Equal(customer.Id, (await check.Orders.AsNoTracking().SingleAsync(o => o.Id == mine)).CustomerId);
        Assert.Null((await check.Orders.AsNoTracking().SingleAsync(o => o.Id == other)).CustomerId);

        // Kullanılmış bağlantının sayfası formsuz "geçersiz" iletisidir; form anahtarı giriş sayfasından alınır.
        var reused = await ConfirmAsync(_factory.CreateNonRedirectingClient(), link, "/hesap/dogrula", Password, formUrl: "/hesap/giris");
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    [Fact]
    public async Task Hatali_parola_bilinmeyen_eposta_ve_dogrulanmamis_hesap_ayni_mesaji_alir_cerez_yazilmaz()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        await TestData.AddCustomerAsync(context, "yeni@example.com", Password, verified: false);

        foreach (var (email, password) in new[] { (Email, "YanlisParola1"), ("yok@example.com", Password), ("yeni@example.com", Password) })
        {
            var response = await LoginAsync(_factory.CreateNonRedirectingClient(), email, password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(CredentialsMessage, await response.Content.ReadAsStringAsync());
            Assert.False(response.Headers.Contains("Set-Cookie") && response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("heryerde.customer=", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public async Task Basarili_giris_30_gun_kalici_httpOnly_musteri_cerezi_yazar_ve_donus_adresine_gider()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var client = _factory.CreateNonRedirectingClient();

        var before = DateTimeOffset.UtcNow;
        var response = await LoginAsync(client, Email, Password, "/hesap/adresler");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/hesap/adresler", response.Headers.Location!.OriginalString);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("heryerde.customer=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        var expires = DateTimeOffset.Parse(Regex.Match(cookie, "expires=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value);
        Assert.InRange(expires, before.AddDays(30).AddMinutes(-2), before.AddDays(30).AddMinutes(2));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/hesap/adresler")).StatusCode);
    }

    [Fact]
    public async Task Parolasiz_giris_baglantisi_15_dakika_gecerli_ve_tek_kullanimlik()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, password: null);
        var client = _factory.CreateNonRedirectingClient();

        var requested = await HtmlForm.PostAsync(client, "/hesap/giris", "/hesap/giris-baglantisi", new() { ["Email"] = Email });
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        Assert.Contains("giriş bağlantısı gönderildi", await requested.Content.ReadAsStringAsync());
        var link = await MailLinkAsync(OutboxType.CustomerLoginLink, "/hesap/baglanti");
        await using (var issued = TestDb.NewContext())
        {
            Assert.Equal(TestClock.Now.AddMinutes(15), (await issued.Customers.AsNoTracking().SingleAsync()).LoginTokenExpiresAt);
        }

        var used = await ConfirmAsync(client, link, "/hesap/baglanti");
        Assert.Equal(HttpStatusCode.Found, used.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/hesap")).StatusCode);

        var again = await ConfirmAsync(_factory.CreateNonRedirectingClient(), link, "/hesap/baglanti", formUrl: "/hesap/giris");
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        // Girişli istemci giriş sayfasından hesaba yönlenir: ikinci bağlantıyı yeni tarayıcı ister.
        await HtmlForm.PostAsync(_factory.CreateNonRedirectingClient(), "/hesap/giris", "/hesap/giris-baglantisi", new() { ["Email"] = Email });
        var second = await MailLinkAsync(OutboxType.CustomerLoginLink, "/hesap/baglanti", latest: true);
        await using (var expire = TestDb.NewContext())
        {
            await expire.Customers.ExecuteUpdateAsync(s => s.SetProperty(c => c.LoginTokenExpiresAt, TestClock.Now.AddSeconds(-1)));
        }

        var expired = await ConfirmAsync(_factory.CreateNonRedirectingClient(), second, "/hesap/baglanti", formUrl: "/hesap/giris");
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
    }

    [Fact]
    public async Task Dogrulanmamis_hesap_giris_baglantisiyla_dogrulanir_kayittaki_kanitlanmamis_parola_silinir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, "SaldirganParola1", verified: false);
        var client = _factory.CreateNonRedirectingClient();

        await HtmlForm.PostAsync(client, "/hesap/giris", "/hesap/giris-baglantisi", new() { ["Email"] = Email });
        var link = await MailLinkAsync(OutboxType.CustomerLoginLink, "/hesap/baglanti");
        Assert.Equal(HttpStatusCode.Found, (await ConfirmAsync(client, link, "/hesap/baglanti")).StatusCode);

        await using var check = TestDb.NewContext();
        var customer = await check.Customers.AsNoTracking().SingleAsync();
        Assert.Equal(TestClock.Now, customer.EmailVerifiedAt);
        Assert.Null(customer.PasswordHash);
        Assert.Contains(CredentialsMessage, await (await LoginAsync(_factory.CreateNonRedirectingClient(), Email, "SaldirganParola1")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Bilinmeyen_epostada_baglanti_ve_sifirlama_istegi_ayni_yaniti_ayni_asgari_surede_verir_posta_gitmez()
    {
        var client = _factory.CreateNonRedirectingClient();

        foreach (var (form, action, message) in new[]
                 {
                     ("/hesap/giris", "/hesap/giris-baglantisi", "giriş bağlantısı gönderildi"),
                     ("/hesap/sifremi-unuttum", "/hesap/sifremi-unuttum", "parola sıfırlama bağlantısı gönderildi")
                 })
        {
            var token = await HtmlForm.AntiforgeryTokenAsync(client, form);
            var started = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.PostAsync(action, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "yok@example.com",
                ["__RequestVerificationToken"] = token
            }));
            started.Stop();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(message, await response.Content.ReadAsStringAsync());
            Assert.True(started.Elapsed >= CustomerAccountController.ResponseFloor - TimeSpan.FromMilliseconds(20), $"{action} {started.Elapsed.TotalMilliseconds} ms");
        }

        await using var context = TestDb.NewContext();
        Assert.Empty(await context.OutboxMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Parola_sifirlama_yeni_parola_koyar_diger_oturumlari_dusurur_baglanti_ikinci_kez_kullanilmaz()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var stale = await SignedInAsync(_factory);
        var client = _factory.CreateNonRedirectingClient();

        await HtmlForm.PostAsync(client, "/hesap/sifremi-unuttum", "/hesap/sifremi-unuttum", new() { ["Email"] = Email });
        var link = await MailLinkAsync(OutboxType.CustomerPasswordReset, "/hesap/sifre-sifirla");
        await using (var issued = TestDb.NewContext())
        {
            Assert.Equal(TestClock.Now.AddMinutes(30), (await issued.Customers.AsNoTracking().SingleAsync()).ResetTokenExpiresAt);
        }

        var reset = await ResetAsync(client, link, "YeniUzunParola7");
        Assert.Equal(HttpStatusCode.Found, reset.StatusCode);
        Assert.Equal("/hesap/giris", reset.Headers.Location!.OriginalString);

        var dropped = await stale.GetAsync("/hesap");
        Assert.Equal(HttpStatusCode.Found, dropped.StatusCode);
        Assert.StartsWith("/hesap/giris", dropped.Headers.Location!.PathAndQuery);
        Assert.Equal(HttpStatusCode.Found, (await LoginAsync(_factory.CreateNonRedirectingClient(), Email, "YeniUzunParola7")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(_factory.CreateNonRedirectingClient(), link, "BaskaUzunParola8")).StatusCode);
    }

    [Fact]
    public async Task Tum_cihazlardan_cik_diger_oturumlari_dusurur_bu_oturum_acik_kalir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var other = await SignedInAsync(_factory);
        var current = await SignedInAsync(_factory);

        var response = await HtmlForm.PostAsync(current, "/hesap/ayarlar", "/hesap/oturumlar/kapat", []);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await other.GetAsync("/hesap")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/hesap")).StatusCode);
    }

    [Fact]
    public async Task Cikis_musteri_cerezini_siler()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var client = await SignedInAsync(_factory);

        var response = await HtmlForm.PostAsync(client, "/hesap", "/hesap/cikis", []);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await client.GetAsync("/hesap")).StatusCode);
    }

    [Theory]
    [InlineData("/hesap")]
    [InlineData("/hesap/adresler")]
    [InlineData("/hesap/favoriler")]
    [InlineData("/hesap/yorumlar")]
    [InlineData("/hesap/ceyiz-listeleri")]
    [InlineData("/hesap/ayarlar")]
    [InlineData("/hesap/sil")]
    public async Task Hesap_sayfalari_girissiz_donus_adresiyle_giris_sayfasina_yonlenir(string path)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/hesap/giris?donus=" + Uri.EscapeDataString(path), response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Musteri_cerezi_yonetime_yonetici_cerezi_hesaba_erisim_vermez()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var customer = await SignedInAsync(_factory);
        var admin = await _factory.CreateSignedInClientAsync();

        var adminPage = await customer.GetAsync("/admin/products");
        var accountPage = await admin.GetAsync("/hesap");

        Assert.Equal(HttpStatusCode.Found, adminPage.StatusCode);
        Assert.Contains("/admin/auth/login", adminPage.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Found, accountPage.StatusCode);
        Assert.Contains("/hesap/giris", accountPage.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Kayitta_saatte_altinci_giriste_dakikada_on_birinci_istek_429_alir()
    {
        var signup = _factory.CreateNonRedirectingClient();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await signup.PostAsync("/hesap/kayit", new FormUrlEncodedContent([]))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await signup.PostAsync("/hesap/kayit", new FormUrlEncodedContent([]))).StatusCode);

        using var other = new AdminWebFactory();
        var login = other.CreateNonRedirectingClient();
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await login.PostAsync("/hesap/giris", new FormUrlEncodedContent([]))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await login.PostAsync("/hesap/giris-baglantisi", new FormUrlEncodedContent([]))).StatusCode);
    }

    [Fact]
    public async Task Ayarlarda_ad_telefon_ve_pazarlama_izni_kaydedilir_izin_degisim_ani_yazilir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var client = await SignedInAsync(_factory);

        var opened = await HtmlForm.PostAsync(client, "/hesap/ayarlar", "/hesap/ayarlar", new()
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["MarketingConsent"] = "true"
        });
        Assert.Equal(HttpStatusCode.Found, opened.StatusCode);
        await using (var check = TestDb.NewContext())
        {
            var customer = await check.Customers.AsNoTracking().SingleAsync();
            Assert.Equal("Ayşe Yılmaz", customer.FullName);
            Assert.Equal("05424970982", customer.Phone);
            Assert.True(customer.MarketingConsent);
            Assert.Equal(TestClock.Now, customer.MarketingConsentAt);
        }

        await HtmlForm.PostAsync(client, "/hesap/ayarlar", "/hesap/ayarlar", new() { ["FullName"] = "Ayşe Yılmaz", ["Phone"] = "05424970982" });
        await using var closed = TestDb.NewContext();
        Assert.False((await closed.Customers.AsNoTracking().SingleAsync()).MarketingConsent);
    }

    // ---------- yardımcılar ----------

    public static Task<HttpResponseMessage> SignUpAsync(HttpClient client, string email, string? password, bool kvkk = true, bool marketing = false)
    {
        var fields = new Dictionary<string, string> { ["Email"] = email, ["Password"] = password ?? string.Empty };
        if (kvkk)
        {
            fields["KvkkConsent"] = "true";
        }

        if (marketing)
        {
            fields["MarketingConsent"] = "true";
        }

        return HtmlForm.PostAsync(client, "/hesap/kayit", "/hesap/kayit", fields);
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string? returnUrl = null)
    {
        var action = returnUrl is null ? "/hesap/giris" : "/hesap/giris?donus=" + Uri.EscapeDataString(returnUrl);
        return await HtmlForm.PostAsync(client, "/hesap/giris", action, new() { ["Email"] = email, ["Password"] = password });
    }

    /// <summary>Doğrulanmış hesapla (<see cref="Email"/> / <see cref="Password"/>) giriş yapmış istemci.</summary>
    public static async Task<HttpClient> SignedInAsync(AdminWebFactory factory, string email = Email, string password = Password)
    {
        var client = factory.CreateNonRedirectingClient();
        var response = await LoginAsync(client, email, password);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return client;
    }

    public static async Task<string> MailLinkAsync(string type, string path, bool latest = false)
    {
        await using var context = TestDb.NewContext();
        var mails = await context.OutboxMessages.AsNoTracking().Where(m => m.Type == type).OrderBy(m => m.Id).ToListAsync();
        var mail = latest ? mails.Last() : Assert.Single(mails);
        var match = Regex.Match(mail.Body, Regex.Escape(path) + "\\?t=([A-Za-z0-9_-]+)");
        Assert.True(match.Success, mail.Body);
        return path + "?t=" + match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string link, string action, string? password = null, string? formUrl = null)
    {
        var fields = new Dictionary<string, string> { ["Token"] = link[(link.IndexOf("?t=", StringComparison.Ordinal) + 3)..] };
        if (password is not null)
        {
            fields["Password"] = password;
        }

        return await HtmlForm.PostAsync(client, formUrl ?? link, action, fields);
    }

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, string link, string password)
        => HtmlForm.PostAsync(client, link, "/hesap/sifre-sifirla", new()
        {
            ["Token"] = link[(link.IndexOf("?t=", StringComparison.Ordinal) + 3)..],
            ["NewPassword"] = password,
            ["ConfirmPassword"] = password
        });

    public static async Task<int> PlaceGuestOrderAsync(HerYerde.DataAccess.Concrete.EntityFramework.Contexts.HerYerdeContext context, string email, string slug)
    {
        var cartManager = TestData.NewCartManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Ürün " + slug, slug, stock: 5);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);
        var (_, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new HerYerde.Business.Dtos.OrderDraft(
            "Ayşe Yılmaz", "05424970982", email, "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, HerYerde.Entities.Enums.PaymentMethod.HavaleEft));
        return result.Data!.Id;
    }
}
