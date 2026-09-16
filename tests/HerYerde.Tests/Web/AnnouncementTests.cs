using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D14 B2: site üstündeki duyuru şeridi; yalnız tarih aralığı içindeki etkin duyuru görünür.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AnnouncementTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Suresi_icindeki_duyuru_site_ustunde_gorunur()
    {
        await using var context = TestDb.NewContext();
        await AddAsync(context, "Kargo bedava", TestClock.Now.AddDays(-1), TestClock.Now.AddDays(1));

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("Kargo bedava", html);
        Assert.Contains("data-announcement", html);
        Assert.Contains("announcement--kiremit", html);
    }

    [Fact]
    public async Task Tarih_disindaki_duyuru_gorunmez()
    {
        await using var context = TestDb.NewContext();
        await AddAsync(context, "Gelecek kampanya", TestClock.Now.AddDays(1), TestClock.Now.AddDays(3));
        await AddAsync(context, "Biten kampanya", TestClock.Now.AddDays(-5), TestClock.Now.AddDays(-1));

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Gelecek kampanya", html);
        Assert.DoesNotContain("Biten kampanya", html);
        Assert.DoesNotContain("data-announcement", html);
    }

    [Fact]
    public async Task Kapatilan_duyuru_gorunmez()
    {
        await using var context = TestDb.NewContext();
        await AddAsync(context, "Kapalı duyuru", TestClock.Now.AddDays(-1), TestClock.Now.AddDays(1), active: false);

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Kapalı duyuru", html);
    }

    [Fact]
    public async Task Yonetici_duyuru_acar_duzenler_ve_siler()
    {
        var admin = await _factory.CreateSignedInClientAsync();

        var created = await HtmlForm.PostAsync(admin, "/admin/duyurular/yeni", "/admin/duyurular/yeni", new Dictionary<string, string>
        {
            ["Text"] = "2500 ₺ üzeri kargo bedava",
            ["Url"] = "/ev",
            ["StartsAt"] = "2026-01-14",
            ["EndsAt"] = "2026-01-20",
            ["Color"] = ((int)AnnouncementColor.Yesil).ToString(),
            ["IsActive"] = "true"
        });

        Assert.Equal(System.Net.HttpStatusCode.Found, created.StatusCode);
        await using var context = TestDb.NewContext();
        var saved = Assert.Single(await new EfAnnouncementDal(context).GetListAsync());
        Assert.Equal("2500 ₺ üzeri kargo bedava", saved.Text);
        Assert.Equal(AnnouncementColor.Yesil, saved.Color);
        Assert.Equal("/ev", saved.Url);

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/")).Content.ReadAsStringAsync();
        Assert.Contains("announcement--yesil", html);

        var deleted = await HtmlForm.PostAsync(admin, "/admin/duyurular", $"/admin/duyurular/{saved.Id}/sil", []);
        Assert.Equal(System.Net.HttpStatusCode.Found, deleted.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Empty(await new EfAnnouncementDal(check).GetListAsync());
    }

    [Fact]
    public async Task Duyuruya_site_disi_baglanti_yazilamaz()
    {
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(admin, "/admin/duyurular/yeni", "/admin/duyurular/yeni", new Dictionary<string, string>
        {
            ["Text"] = "Dışarı",
            ["Url"] = "https://baska-site.test/kampanya",
            ["StartsAt"] = "2026-01-14",
            ["EndsAt"] = "2026-01-20",
            ["Color"] = ((int)AnnouncementColor.Kiremit).ToString(),
            ["IsActive"] = "true"
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Empty(await new EfAnnouncementDal(context).GetListAsync());
    }

    private static async Task AddAsync(
        HerYerdeContext context,
        string text,
        DateTime startsAt,
        DateTime endsAt,
        bool active = true)
    {
        await new EfAnnouncementDal(context).AddAsync(new Announcement
        {
            Text = text,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Color = AnnouncementColor.Kiremit,
            IsActive = active,
            CreatedAt = TestClock.Now
        });
        await new EfUnitOfWork(context).SaveChangesAsync();
    }
}
