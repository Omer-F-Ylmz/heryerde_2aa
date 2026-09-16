using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D16 A3 (D15 kalanları): kupon ve duyuru tabloları FRONT kit tablo bileşeniyle; Shop:Acronyms kısaltmaları ad
/// önerisinde büyük kalır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class D15LeftoverTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/admin/kuponlar")]
    [InlineData("/admin/duyurular")]
    public async Task Kupon_ve_duyuru_listeleri_front_kit_tablo_bilesenini_kullanir(string url)
    {
        await using (var context = TestDb.NewContext())
        {
            await new EfCouponDal(context).AddAsync(new Coupon
            {
                Code = "HOSGELDIN",
                Kind = CouponKind.Yuzde,
                Value = 10m,
                StartsAt = TestClock.Now.AddDays(-1),
                EndsAt = TestClock.Now.AddDays(7),
                IsActive = true,
                CreatedAt = TestClock.Now
            });
            await new EfAnnouncementDal(context).AddAsync(new Announcement
            {
                Text = "Kargo bizden",
                StartsAt = TestClock.Now.AddDays(-1),
                EndsAt = TestClock.Now.AddDays(7),
                Color = AnnouncementColor.Kiremit,
                IsActive = true,
                CreatedAt = TestClock.Now
            });
            await new EfUnitOfWork(context).SaveChangesAsync();
        }

        var client = await _factory.CreateSignedInClientAsync();
        var html = await (await client.GetAsync(url)).Content.ReadAsStringAsync();

        Assert.Contains("class=\"page-head\"", html);
        Assert.Matches("<div class=\"table-wrap[^\"]*\">\\s*<table class=\"table\">", html);
        Assert.DoesNotContain("admin-table", html);
        Assert.DoesNotContain("admin-head", html);
    }

    [Fact]
    public async Task Kisaltmalar_ad_onerisinde_buyuk_harf_kalir()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "KRİSTAL GÖRÜNÜMLÜ LED MASA LAMBASI USB", "kristal-led");
        }

        var client = await _factory.CreateSignedInClientAsync();
        var response = await client.GetAsync($"/admin/products/edit/{productId.ToString(CultureInfo.InvariantCulture)}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(new Regex("field__hint-value\">Kristal görünümlü LED masa lambası USB<"), html);
    }
}
