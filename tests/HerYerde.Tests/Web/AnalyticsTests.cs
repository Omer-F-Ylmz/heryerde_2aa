using System.Globalization;
using System.Net;
using HerYerde.Business.Rules;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using HerYerde.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HerYerde.Tests.Web;

/// <summary>D16 A1: çerezsiz analitik — sayfa görüntüleme IP/UA'sız ve bellekte birikip tek toplu yazımla kaydedilir; bot ve
/// dışarıdaki yollar sayılmaz; sepet/ödeme/sipariş/arama sunucudan, WhatsApp/Instagram tıklaması beacon'dan gelir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AnalyticsTests : IAsyncLifetime
{
    private const string IPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";
    private const string Desktop = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36";

    private readonly CountingFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Sayfa_goruntuleme_ip_ve_tarayici_kimligi_olmadan_yol_kaynak_utm_cihaz_ve_saatle_yazilir()
    {
        await SeedProductAsync();
        var client = Client(IPhone);
        var request = new HttpRequestMessage(HttpMethod.Get, "/ev?utm_source=Instagram&utm_medium=Bio&utm_campaign=Eylul");
        request.Headers.Referrer = new Uri("https://www.instagram.com/p/abc");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
        await FlushAsync();

        await using var context = TestDb.NewContext();
        var view = Assert.Single(await context.PageViews.ToListAsync());
        Assert.Equal(AnalyticsEvent.View, view.Event);
        Assert.Equal("/ev", view.Path);
        Assert.Equal("instagram.com", view.ReferrerHost);
        Assert.Equal(("instagram", "bio", "eylul"), (view.UtmSource, view.UtmMedium, view.UtmCampaign));
        Assert.Equal("mobil", view.Device);
        // TestClock 2026-01-15 09:30Z = İstanbul 12:30.
        Assert.Equal((new DateTime(2026, 1, 15), (byte)12, true), (view.Day, view.Hour, view.HalfHour));
        Assert.Equal(
            ["Id", "Event", "Path", "ReferrerHost", "UtmSource", "UtmMedium", "UtmCampaign", "Device", "Day", "Hour", "HalfHour"],
            typeof(PageView).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task Bot_bos_kimlik_ve_disaridaki_yollar_kaydedilmez()
    {
        await SeedProductAsync();

        await Client("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)").GetAsync("/ev");
        await Client(string.Empty).GetAsync("/ev");
        var desktop = Client(Desktop);
        await desktop.GetAsync("/feeds/google.xml");
        await desktop.GetAsync("/health");
        await desktop.GetAsync("/admin/auth/login");
        await desktop.GetAsync("/boyle-bir-sayfa-yok");
        await desktop.GetAsync("/ara/oner?q=tencere");
        await FlushAsync();

        await using var context = TestDb.NewContext();
        Assert.Empty(await context.PageViews.ToListAsync());
    }

    [Fact]
    public async Task Kayitlar_bellekte_birikir_otuz_saniyede_bir_tek_toplu_komutla_yazilir()
    {
        await SeedProductAsync();
        var client = Client(Desktop);
        foreach (var path in new[] { "/", "/ev", "/urun/celik-tencere", "/ara?q=tencere", "/sepet" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        }

        await using (var before = TestDb.NewContext())
        {
            Assert.Equal(0, await before.PageViews.CountAsync());
        }

        _factory.Counter.Reset();
        Assert.Equal(6, await FlushAsync());

        // EF altı satırı tek toplu komutta yazar (SQL Server'da kimlik dönen MERGE); satır başına ayrı INSERT yok.
        Assert.Single(_factory.Counter.Commands);
        Assert.Contains("[page_view]", _factory.Counter.Commands[0], StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromSeconds(30), AnalyticsHostedService.Interval);
        await using var after = TestDb.NewContext();
        Assert.Equal(6, await after.PageViews.CountAsync());
    }

    [Fact]
    public async Task Sepete_ekleme_odeme_baslangici_siparis_ve_arama_sunucudan_olay_olarak_yazilir()
    {
        var productId = await SeedProductAsync();
        var client = Client(Desktop);

        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(CultureInfo.InvariantCulture),
            ["quantity"] = "1"
        });
        var placed = await HtmlForm.PostAsync(client, "/odeme", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)PaymentMethod.KapidaOdeme).ToString(CultureInfo.InvariantCulture),
            ["LegalConsent"] = "true"
        });
        Assert.Equal(HttpStatusCode.Found, placed.StatusCode);
        await client.GetAsync("/ara?q=tencere");
        await FlushAsync();

        await using var context = TestDb.NewContext();
        var events = (await context.PageViews.Where(p => p.Event != AnalyticsEvent.View).ToListAsync())
            .GroupBy(p => p.Event)
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(
            new Dictionary<string, int> { [AnalyticsEvent.AddToCart] = 1, [AnalyticsEvent.Checkout] = 1, [AnalyticsEvent.Order] = 1, [AnalyticsEvent.Search] = 1 },
            events);
    }

    [Fact]
    public async Task Whatsapp_tiklamasi_beacon_ile_yazilir_bilinmeyen_olay_reddedilir()
    {
        await SeedProductAsync();
        var client = Client(Desktop);

        var html = await (await client.GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();
        Assert.Matches("<a[^>]*href=\"https://wa.me[^\"]*\"[^>]*data-event=\"whatsapp\"", html);
        Assert.Matches("<a[^>]*href=\"https://instagram.com/[^\"]*\"[^>]*data-event=\"instagram\"", html);

        var clicked = await client.PostAsync("/olay", new FormUrlEncodedContent(new Dictionary<string, string> { ["ad"] = "whatsapp", ["yol"] = "/urun/celik-tencere" }));
        var forged = await client.PostAsync("/olay", new FormUrlEncodedContent(new Dictionary<string, string> { ["ad"] = AnalyticsEvent.Order, ["yol"] = "/odeme" }));

        Assert.Equal(HttpStatusCode.NoContent, clicked.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        await FlushAsync();
        await using var context = TestDb.NewContext();
        var click = Assert.Single(await context.PageViews.Where(p => p.Event != AnalyticsEvent.View).ToListAsync());
        Assert.Equal((AnalyticsEvent.WhatsApp, "/urun/celik-tencere"), (click.Event, click.Path));
    }

    [Fact]
    public async Task Yonetim_analitik_sayfasi_gunluk_urun_kaynak_huni_ve_tiklamalari_gosterir()
    {
        await SeedProductAsync();
        await using (var context = TestDb.NewContext())
        {
            var day = new DateTime(2026, 1, 14);
            context.PageViews.AddRange(
                new PageView { Event = AnalyticsEvent.View, Path = "/urun/celik-tencere", ReferrerHost = "instagram.com", Device = "mobil", Day = day, Hour = 10 },
                new PageView { Event = AnalyticsEvent.View, Path = "/ev", Device = "masaustu", Day = day, Hour = 11 },
                new PageView { Event = AnalyticsEvent.AddToCart, Path = "/sepet/ekle", Device = "mobil", Day = day, Hour = 10 },
                new PageView { Event = AnalyticsEvent.WhatsApp, Path = "/urun/celik-tencere", Device = "mobil", Day = day, Hour = 10 });
            await context.SaveChangesAsync();
        }

        var anonymous = await _factory.CreateNonRedirectingClient().GetAsync("/admin/analitik");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);

        var admin = await _factory.CreateSignedInClientAsync();
        var html = await (await admin.GetAsync("/admin/analitik?baslangic=2026-01-01&bitis=2026-01-15")).Content.ReadAsStringAsync();

        foreach (var section in new[] { "gunluk", "urunler", "kategoriler", "kaynaklar", "huni", "tiklamalar" })
        {
            Assert.Contains($"id=\"{section}\"", html);
        }

        Assert.Contains("Çelik Tencere", html);
        Assert.Contains("instagram.com", html);
    }

    private HttpClient Client(string userAgent)
    {
        var client = _factory.CreateNonRedirectingClient();
        client.DefaultRequestHeaders.UserAgent.Clear();
        if (userAgent.Length > 0)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        }

        return client;
    }

    [Fact]
    public async Task Cerez_politikasi_aydinlatma_ve_envanter_cerezsiz_analitigi_ve_yerel_depolama_istisnasini_yazar()
    {
        var client = _factory.CreateNonRedirectingClient();
        var cookies = await (await client.GetAsync("/yasal/cerez-politikasi")).Content.ReadAsStringAsync();
        var kvkk = await (await client.GetAsync("/yasal/kvkk-aydinlatma")).Content.ReadAsStringAsync();
        var inventory = RepoFile.ReadAllText("docs", "veri-envanteri.md");

        Assert.Contains("Analitik (çerezsiz)", cookies);
        Assert.Contains("heryerde.ziyaret", cookies);
        Assert.Contains("heryerde.ipucu-kapatildi", cookies);
        Assert.DoesNotContain("Sitede üçüncü taraf, analitik, reklam ya da izleme çerezi yoktur.", cookies);
        Assert.Contains("Ziyaret istatistiği", kvkk);
        Assert.Contains("`page_view`", inventory);
        Assert.Contains("`analytics_daily`", inventory);
        Assert.Contains("`localStorage`", inventory);
        Assert.NotEqual("2026-09-16.2", HerYerde.Business.LegalDocs.Version);
    }

    private Task<int> FlushAsync() => _factory.Services.GetRequiredService<AnalyticsWriter>().FlushAsync();

    private static async Task<int> SeedProductAsync()
    {
        await using var context = TestDb.NewContext();
        if (await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere") is { } existing)
        {
            return existing.Id;
        }

        return await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m);
    }
}
