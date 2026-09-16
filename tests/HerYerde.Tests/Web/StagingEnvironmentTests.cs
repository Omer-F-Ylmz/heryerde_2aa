using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using HerYerde.Web.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sentry.Serilog;

namespace HerYerde.Tests.Web;

/// <summary>D16 C: staging ortamı — Basic Auth kapısı (STAGING_USER/STAGING_PASS; ayarsızsa kapalı kalır), her yanıtta noindex,
/// robots her şeyi kapatır, Sentry ortamı "staging", İyzico sandbox, gerçek SMTP yok. Üretimde kapı ve noindex yoktur.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class StagingEnvironmentTests : IAsyncLifetime
{
    public const string User = "onizleme";
    public const string Password = "staging-parola-42";

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Staging_kimliksiz_istek_401_ve_basic_ister_saglik_ucu_acik()
    {
        using var factory = new StagingFactory(User, Password);
        var client = factory.CreateNonRedirectingClient();

        var home = await client.GetAsync("/");
        var css = await client.GetAsync("/css/site.css");
        var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.Unauthorized, home.StatusCode);
        Assert.Equal("Basic", Assert.Single(home.Headers.WwwAuthenticate).Scheme);
        Assert.Equal(HttpStatusCode.Unauthorized, css.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Staging_dogru_kimlikle_sayfa_acilir_yanlis_kimlikle_401()
    {
        using var factory = new StagingFactory(User, Password);

        var right = factory.CreateNonRedirectingClient();
        right.DefaultRequestHeaders.Authorization = Basic(User, Password);
        var wrong = factory.CreateNonRedirectingClient();
        wrong.DefaultRequestHeaders.Authorization = Basic(User, Password + "x");

        Assert.Equal(HttpStatusCode.OK, (await right.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Staging_kimlik_ayarsizsa_kapi_kapali_kalir()
    {
        using var factory = new StagingFactory(user: "", password: "");
        var client = factory.CreateNonRedirectingClient();
        client.DefaultRequestHeaders.Authorization = Basic("", "");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Staging_her_yanitta_noindex_basligi_robots_her_seyi_kapatir()
    {
        using var factory = new StagingFactory(User, Password);
        var anonymous = factory.CreateNonRedirectingClient();
        var client = factory.CreateNonRedirectingClient();
        client.DefaultRequestHeaders.Authorization = Basic(User, Password);

        var denied = await anonymous.GetAsync("/");
        var home = await client.GetAsync("/");
        var robots = await client.GetAsync("/robots.txt");
        var body = (await robots.Content.ReadAsStringAsync()).ReplaceLineEndings("\n");

        Assert.Equal("noindex, nofollow", string.Join(", ", denied.Headers.GetValues("X-Robots-Tag")));
        Assert.Equal("noindex, nofollow", string.Join(", ", home.Headers.GetValues("X-Robots-Tag")));
        Assert.Contains("\nDisallow: /\n", "\n" + body + "\n");
        Assert.DoesNotContain("Sitemap:", body);
        Assert.DoesNotContain("Allow: /feeds/", body);
    }

    [Fact]
    public async Task Staging_bilerek_kapali_smtp_outbox_birikmesi_hazirligi_dusurmez()
    {
        // Staging'de SMTP yok: postalar kuyrukta bekler. Üretimde aynı birikme 503'tür (ReadinessTests).
        await using (var context = TestDb.NewContext())
        {
            context.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxType.OrderPlaced,
                To = "ayse@example.com",
                Subject = "Siparişiniz alındı",
                Body = "gövde",
                Status = OutboxStatus.Bekliyor,
                CreatedAt = TestClock.Now.AddHours(-2)
            });
            await context.SaveChangesAsync();
        }

        using var factory = new StagingFactory(User, Password);
        var response = await factory.CreateNonRedirectingClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Uretimde_kapi_ve_noindex_yok()
    {
        using var factory = new ProductionFactory();
        var response = await factory.CreateNonRedirectingClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Robots-Tag"));
    }

    [Fact]
    public void Staging_ayarlari_sentry_ortami_iyzico_sandbox_ve_smtp_kapali()
    {
        using var factory = new StagingFactory(User, Password);
        var services = factory.Services;

        Assert.Equal("staging", services.GetRequiredService<IConfiguration>()["Sentry:Environment"]);
        Assert.Equal("https://sandbox-api.iyzipay.com", services.GetRequiredService<IOptions<IyzicoSettings>>().Value.BaseUrl);
        Assert.Equal(["https://sandbox-api.iyzipay.com"], services.GetRequiredService<IOptions<IyzicoSettings>>().Value.CspSources);
        Assert.False(services.GetRequiredService<INotificationSender>().IsConfigured);
    }

    [Fact]
    public void Sentry_olaylari_ayardaki_ortam_adini_tasir()
    {
        var options = new SentrySerilogOptions();

        SentryReporting.Configure(options, "https://anahtar@sentry.invalid/1", transport: null, environment: "staging");

        Assert.Equal("staging", options.Environment);
    }

    [Theory]
    [InlineData(new[] { "--migrate" }, true)]
    [InlineData(new[] { "--ithal", "x" }, false)]
    [InlineData(new string[0], false)]
    public void Migrate_komutu_yalniz_bayrakla_istenir(string[] args, bool expected)
        => Assert.Equal(expected, MigrateCommand.Requested(args));

    public static AuthenticationHeaderValue Basic(string user, string password)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)));

    /// <summary>Staging ortamı: appsettings.Staging.json okunur, kapı kimliği ortam değişkeni yerine ayardan verilir.</summary>
    public sealed class StagingFactory(string user, string password) : AdminWebFactory
    {
        protected override string Environment => "Staging";

        protected override void Configure(Dictionary<string, string?> settings)
        {
            settings["AllowedHosts"] = "localhost;127.0.0.1";
            settings["StagingGate:User"] = user;
            settings["StagingGate:Password"] = password;
        }
    }
}
