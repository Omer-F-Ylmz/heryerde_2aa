using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HerYerde.Business.Notifications;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace HerYerde.Tests.Web;

/// <summary>YAYIN-KAPI: yayını bloke eden küçükler — sahte yorum yok, müşteriye görünen metinde yer tutucu kalmaz (Production'da
/// hazırlık 503), ilk yönetici parolasını değiştirir ve iki adımlı doğrulama kurar, footer ödeme notu açık yöntemlerden, il-ilçe
/// resmî kaynaktan, lisans notları tam.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReleaseGateTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ana_sayfa_onayli_yorum_yoksa_yorum_bolumu_render_edilmez()
    {
        await AddReviewAsync("Onay bekleyen yorum metni", approved: false);
        using var factory = new AdminWebFactory();

        var html = await factory.CreateClient().GetStringAsync("/");

        Assert.DoesNotContain("id=\"dm\"", html);
        Assert.DoesNotContain("Müşterilerimizden", html);
        Assert.DoesNotContain("Onay bekleyen yorum metni", html);
    }

    [Fact]
    public async Task Ana_sayfa_onayli_yorumla_bolum_gercek_yorumu_gosterir_sabit_alintilar_kaldirildi()
    {
        await AddReviewAsync("Tencere çok sağlam, tavsiye ederim.", approved: true);
        using var factory = new AdminWebFactory();

        var html = WebUtility.HtmlDecode(await factory.CreateClient().GetStringAsync("/"));

        Assert.Contains("id=\"dm\"", html);
        Assert.Contains("Tencere çok sağlam, tavsiye ederim.", html);
        Assert.Contains("Çelik Tencere yorumu", html);
        Assert.False(File.Exists(RepoFile.PathOf("docs", "testimonials.json")));
        Assert.DoesNotContain("testimonials.json", RepoFile.ReadAllText("HerYerde.Web", "HerYerde.Web.csproj"));
        Assert.DoesNotContain("Tencereler tam fotoğraftaki gibi geldi", RepoFile.ReadAllText("HerYerde.Web", "Views", "Styleguide", "Index.cshtml"));
    }

    [Fact]
    public async Task Uretimde_musteriye_gorunen_metinde_yer_tutucu_varsa_hazirlik_503()
    {
        using var factory = new ProductionFactory();

        var response = await factory.CreateNonRedirectingClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Stagingde_yer_tutucu_hazirligi_dusurmez_kaynaklariyla_uyari_loglanir()
    {
        using var factory = new AuditLogFactory("Staging");

        var response = await factory.CreateNonRedirectingClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warning = Assert.Single(factory.Sink.Events, e => e.Level == LogEventLevel.Warning && e.MessageTemplate.Text.Contains("yer tutucu"));
        var message = warning.RenderMessage();
        foreach (var source in new[] { "yasal/kvkk-aydinlatma", "yasal/cayma-formu", "sözleşme PDF'i (mesafeli-satis-sozlesmesi)", "hakkimizda", "iletisim", "sss" })
        {
            Assert.Contains(source, message);
        }
    }

    [Fact]
    public void Yer_tutucu_taramasi_tum_posta_sablonlarini_ornek_veriyle_uretir()
    {
        var templates = typeof(NotificationTemplates).GetMethods(BindingFlags.Public | BindingFlags.Static).Select(m => m.Name).Order().ToList();

        var samples = PlaceholderAudit.MailSamples().ToList();

        Assert.Equal(templates, samples.Select(s => s.Name).Order().ToList());
        Assert.All(samples, sample =>
        {
            Assert.False(string.IsNullOrWhiteSpace(sample.Subject));
            Assert.Contains("</", sample.Body);
        });
    }

    [Fact]
    public async Task Tohumlanan_ilk_yonetici_ilk_giriste_parola_sayfasina_yonlendirilir()
    {
        using var factory = new FirstLoginFactory();
        var client = factory.CreateNonRedirectingClient();

        var login = await HtmlForm.PostAsync(client, "/admin/auth/login", "/admin/auth/login", new Dictionary<string, string>
        {
            ["Email"] = AdminWebFactory.AdminEmail,
            ["Password"] = AdminWebFactory.AdminPassword
        });
        var products = await client.GetAsync("/admin/products");

        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        Assert.Equal(AdminPolicy.ChangePasswordPath, PathOf(login.Headers.Location));
        Assert.Equal(HttpStatusCode.Found, products.StatusCode);
        Assert.Equal(AdminPolicy.ChangePasswordPath, PathOf(products.Headers.Location));
    }

    [Fact]
    public async Task Require2FA_acikken_iki_adim_kurmamis_yonetici_yalniz_kurulum_sayfasina_erisir()
    {
        using var factory = new Require2FaFactory();
        var client = await factory.CreateSignedInClientAsync();

        var products = await client.GetAsync("/admin/products");
        var setup = await client.GetAsync("/admin/iki-adim");
        await using (var context = TestDb.NewContext())
        {
            await context.AdminUsers.Where(a => a.Email == AdminWebFactory.AdminEmail).ExecuteUpdateAsync(s => s.SetProperty(a => a.TotpEnabled, true));
        }

        var afterSetup = await client.GetAsync("/admin/products");

        Assert.Equal(HttpStatusCode.Found, products.StatusCode);
        Assert.Equal("/admin/iki-adim", PathOf(products.Headers.Location));
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterSetup.StatusCode);
    }

    [Fact]
    public async Task Uretimde_Require2FA_varsayilan_acik()
    {
        using var factory = new ProductionFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://" + ProductionFactory.Host)
        });
        var login = await HtmlForm.PostAsync(client, "/admin/auth/login", "/admin/auth/login", new Dictionary<string, string>
        {
            ["Email"] = AdminWebFactory.AdminEmail,
            ["Password"] = AdminWebFactory.AdminPassword
        });

        var products = await client.GetAsync("/admin/products");

        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        Assert.Equal(HttpStatusCode.Found, products.StatusCode);
        Assert.Equal("/admin/iki-adim", PathOf(products.Headers.Location));
    }

    [Fact]
    public async Task Footer_odeme_notu_acik_yontemleri_sayar_kart_yalniz_kart_acikken()
    {
        using var withoutCard = new CardFactory(enabled: false);
        using var withCard = new CardFactory(enabled: true);

        var cardless = WebUtility.HtmlDecode(await withoutCard.CreateClient().GetStringAsync("/sss"));
        var card = WebUtility.HtmlDecode(await withCard.CreateClient().GetStringAsync("/sss"));

        Assert.Contains("<span class=\"site-footer__note\">Kapıda ödeme · Havale/EFT · Fiyatlara KDV dahildir</span>", cardless);
        Assert.Contains("<span class=\"site-footer__note\">Kapıda ödeme · Havale/EFT · Kart · Fiyatlara KDV dahildir</span>", card);
    }

    [Fact]
    public void Il_ilce_verisi_resmi_kaynaktan_81_il_kaynak_ve_kosullar_lisans_notunda()
    {
        using var json = JsonDocument.Parse(RepoFile.ReadAllText("HerYerde.Web", "wwwroot", "data", "il-ilce.json"));
        var source = json.RootElement.GetProperty("kaynak");
        var address = source.GetProperty("adres").GetString()!;
        var directory = new ProvinceDirectory(RepoFile.PathOf("HerYerde.Web", "wwwroot"));
        var notes = RepoFile.ReadAllText("docs", "lisans-notlari.md");

        Assert.Matches("PTT|TÜİK|İçişleri|Nüfus ve Vatandaşlık", source.GetProperty("ad").GetString());
        Assert.StartsWith("https://", address);
        Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("kosullar").GetString()));
        Assert.Equal(81, directory.Names.Count);
        Assert.Equal(973, directory.Names.Sum(name => directory.DistrictsOf(name).Count));
        Assert.Contains("il-ilce.json", notes);
        Assert.Contains(address, notes);
    }

    [Fact]
    public void Lisans_notlari_tum_dogrudan_bagimliliklari_fontlari_ve_veriyi_kapsar()
    {
        var notes = RepoFile.ReadAllText("docs", "lisans-notlari.md");
        var root = RepoFile.PathOf();
        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"));
        var packages = projects
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "<PackageReference Include=\"(?<id>[^\"]+)\" Version=\"(?<version>[^\"]+)\"").Cast<Match>())
            .Select(match => (Id: match.Groups["id"].Value, Version: match.Groups["version"].Value))
            .Distinct()
            .ToList();
        using var npm = JsonDocument.Parse(RepoFile.ReadAllText("package.json"));
        var fonts = Directory.GetFiles(RepoFile.PathOf("HerYerde.Web", "wwwroot", "fonts"), "*.woff2")
            .Select(path => Path.GetFileName(path).Split('-')[0])
            .Distinct();

        Assert.NotEmpty(packages);
        var missing = packages
            .Where(p => !Regex.IsMatch(notes, Regex.Escape(p.Id) + @"[\s*`|]+" + Regex.Escape(p.Version)))
            .Select(p => $"{p.Id} {p.Version}")
            .Concat(npm.RootElement.GetProperty("devDependencies").EnumerateObject().Select(d => d.Name).Where(name => !notes.Contains(name)))
            .Concat(fonts.Where(font => !notes.Contains(font, StringComparison.OrdinalIgnoreCase)))
            .Concat(new[] { "il-ilce.json" }.Where(data => !notes.Contains(data)))
            .ToList();
        Assert.True(missing.Count == 0, "Lisans notunda eksik: " + string.Join(", ", missing));
    }

    /// <summary>CI'da prod imajını derleyen işler (smoke, restore-check, zap-scan) taslak metni örnek değerle doldurup kalkar; gerçek
    /// yayın (deploy.yml) doldurmaz, yer tutucu kalmışsa smoke kırmızıdır. ZAP tarama hesabı ilk girişte parolasını değiştirir,
    /// iki adımlı doğrulama taramada kapalıdır.</summary>
    [Fact]
    public void Ci_prod_imaji_taslak_doldurup_kalkar_yayin_doldurmaz_zap_hesabi_ilk_giris_parolasini_degistirir()
    {
        var ci = RepoFile.ReadAllText(".github", "workflows", "ci.yml");
        string Job(string name)
        {
            var start = ci.IndexOf($"\n  {name}:\n", StringComparison.Ordinal);
            var next = Regex.Match(ci[(start + 1)..], "\n  [a-z-]+:\n");
            return next.Success ? ci.Substring(start, next.Index + 1) : ci[start..];
        }

        foreach (var job in new[] { "zap-scan", "restore-check", "smoke" })
        {
            var body = Job(job);
            var fill = body.IndexOf("bash tools/ci-taslak-doldur.sh", StringComparison.Ordinal);
            Assert.True(fill >= 0, job + " taslak doldurmuyor");
            Assert.True(fill < body.IndexOf("up -d --build", StringComparison.Ordinal), job + " doldurmadan derliyor");
        }

        Assert.DoesNotContain("ci-taslak-doldur", RepoFile.ReadAllText(".github", "workflows", "deploy.yml"));
        Assert.True(File.Exists(RepoFile.PathOf("tools", "ci-taslak-doldur.sh")));
        Assert.Contains("Admin__Require2FA: \"false\"", RepoFile.ReadAllText("docker-compose.zap.yml"));
        Assert.Contains("/admin/sifre", RepoFile.ReadAllText(".zap", "yonetim-cerezi.sh"));
    }

    private static async Task AddReviewAsync(string comment, bool approved)
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        context.ProductReviews.Add(new ProductReview
        {
            ProductId = productId,
            Name = "Ayten B.",
            Rating = 5,
            Comment = comment,
            IsApproved = approved,
            CreatedAt = TestClock.Now
        });
        await context.SaveChangesAsync();
    }

    private static string? PathOf(Uri? location)
        => location is null ? null : location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;

    /// <summary>Uygulamanın Serilog hattını bellekte toplar; verilen ortamda açılır.</summary>
    private sealed class AuditLogFactory(string environment) : AdminWebFactory
    {
        public CollectingSink Sink { get; } = new();

        protected override string Environment => environment;

        protected override void Configure(Dictionary<string, string?> settings) => settings["AllowedHosts"] = "localhost;127.0.0.1";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(Sink));
        }
    }

    /// <summary>Tohumlanan yönetici henüz ilk girişini yapmamış.</summary>
    private sealed class FirstLoginFactory : AdminWebFactory
    {
        protected override bool SeededAdminPasswordChanged => false;
    }

    private sealed class Require2FaFactory : AdminWebFactory
    {
        protected override void Configure(Dictionary<string, string?> settings) => settings["Admin:Require2FA"] = "true";
    }

    /// <summary>İyzico anahtarları dolu ya da açıkça boş (yerel appsettings.Development.json'daki anahtar sızmasın): kartla ödeme açık/kapalı.</summary>
    private sealed class CardFactory(bool enabled) : AdminWebFactory
    {
        protected override void Configure(Dictionary<string, string?> settings)
        {
            settings["Iyzico:ApiKey"] = enabled ? "sandbox-api-key" : "";
            settings["Iyzico:SecretKey"] = enabled ? "sandbox-secret-key" : "";
        }
    }
}
