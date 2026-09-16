using System.Net;
using System.Text.RegularExpressions;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Web;

/// <summary>D15 B3: listeleme süzgeçleri (marka, özellik değerleri, stokta olanlar, kampanyalı) SQL'de, sorgu dizesinde;
/// sayfalama/sıralama bağlantılarında korunur, panel seçenekleri sayılarıyla çizilir, sayım isteği yalnız toplamı döner.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ListingFilterTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Marka_suzgeci_yalniz_secilen_markalari_listeler()
    {
        await SeedAsync();

        Assert.Equal(["celik-tencere", "dokum-guvec"], await SlugsAsync("/ev?marka=tac"));
        Assert.Equal(["celik-tencere", "dokum-guvec", "dokum-tava"], await SlugsAsync("/ev?marka=tac&marka=karaca"));
    }

    [Fact]
    public async Task Ayni_ozellikte_degerler_veya_farkli_ozellikler_ve_ile_birlesir()
    {
        await SeedAsync();

        Assert.Equal(["dokum-guvec", "dokum-tava"], await SlugsAsync("/ev?oz=Malzeme:Döküm"));
        Assert.Equal(["celik-tencere", "dokum-guvec", "dokum-tava"], await SlugsAsync("/ev?oz=Malzeme:Döküm&oz=Malzeme:Çelik"));
        Assert.Empty(await SlugsAsync("/ev?oz=Malzeme:Döküm&oz=Renk:Gri"));
        Assert.Equal(["dokum-guvec"], await SlugsAsync("/ev?marka=tac&oz=Malzeme:Döküm"));
    }

    [Fact]
    public async Task Stokta_olanlar_tukenmis_urunu_ve_tum_varyantlari_bitmis_urunu_gostermez()
    {
        await SeedAsync();

        Assert.Equal(["cam-surahi", "celik-tencere", "dokum-guvec"], await SlugsAsync("/ev?stok=1"));
    }

    [Fact]
    public async Task Kampanyali_yalniz_suren_kampanyayi_gosterir()
    {
        await SeedAsync();

        Assert.Equal(["dokum-guvec"], await SlugsAsync("/ev?kampanya=1"));
    }

    [Fact]
    public async Task Suzgecler_siralama_ve_sayfalama_baglantilarinda_korunur()
    {
        await using (var context = TestDb.NewContext())
        {
            var tac = await TestData.AddBrandAsync(context, "TAÇ", "tac");
            for (var i = 1; i <= 25; i++)
            {
                await TestData.SetBrandAsync(context, await TestData.AddHomeProductAsync(context, $"Tabak {i}", $"tabak-{i}", stock: 3), tac);
            }
        }

        var html = await PageAsync("/ev?marka=tac&stok=1");

        Assert.Matches("href=\"/ev\\?sirala=fiyat[^\"]*marka=tac[^\"]*stok=1\"", html);
        Assert.Matches("href=\"/ev\\?[^\"]*marka=tac[^\"]*stok=1[^\"]*sayfa=2\"", html);
    }

    [Fact]
    public async Task Sayim_istegi_yalniz_sonuc_sayisini_doner()
    {
        await SeedAsync();

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/ev?marka=tac&sayim=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("2", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Suzgec_paneli_secenekleri_sayilari_ve_secili_durumuyla_cizer()
    {
        await SeedAsync();

        var html = await PageAsync("/ev?marka=tac");

        Assert.Matches("name=\"marka\" value=\"tac\"[^>]*checked", html);
        Assert.DoesNotMatch("name=\"marka\" value=\"karaca\"[^>]*checked", html);
        Assert.Matches("value=\"tac\"[^>]*/>\\s*<span[^>]*>TAÇ</span>\\s*<span class=\"filter-option__count\">2</span>", html);
        Assert.Matches("value=\"Malzeme:Döküm\"[^>]*/>\\s*<span[^>]*>Döküm</span>\\s*<span class=\"filter-option__count\">2</span>", html);
    }

    /// <summary>D16 A3: "Stokta olanlar" ve "Kampanyalı" seçenekleri de kapsamdaki ürün sayısını yazar (A, C, D stokta; C kampanyada).</summary>
    [Fact]
    public async Task Stokta_olanlar_ve_kampanyali_secenekleri_sayilarini_yazar()
    {
        await SeedAsync();

        var html = await PageAsync("/ev");

        Assert.Matches(@"name=""stok"" value=""1""[^>]*/>\s*<span[^>]*>Stokta olanlar</span>\s*<span class=""filter-option__count"">3</span>", html);
        Assert.Matches(@"name=""kampanya"" value=""1""[^>]*/>\s*<span[^>]*>Kampanyalı</span>\s*<span class=""filter-option__count"">1</span>", html);
    }

    [Fact]
    public async Task Arama_ve_marka_sayfasinda_da_suzgec_uygulanir()
    {
        await SeedAsync();

        Assert.Equal(["dokum-tava"], await SlugsAsync("/ara?q=döküm&marka=karaca"));
        Assert.Equal(["celik-tencere"], await SlugsAsync("/marka/tac?oz=Malzeme:Çelik"));
    }

    private async Task<string> PageAsync(string url)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Kart ızgarasındaki ürünler (öneri rafı hariç), ada göre sıralı.</summary>
    private async Task<List<string>> SlugsAsync(string url)
    {
        var html = await PageAsync(url);
        var grid = html.IndexOf("class=\"grid\"", StringComparison.Ordinal);
        if (grid < 0)
        {
            return [];
        }

        var end = html.IndexOf("</main>", grid, StringComparison.Ordinal);
        return Regex.Matches(html[grid..end], "class=\"card__title\"><a href=\"/urun/([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>A: TAÇ, çelik, gri, stok 5 · B: Karaca, döküm, kırmızı, stok 0 · C: TAÇ, döküm, kırmızı, stok takipsiz,
    /// süren kampanya · D: markasız, stok 3, bitmiş kampanya · E: tek varyantı tükenmiş.</summary>
    private static async Task SeedAsync()
    {
        await using var context = TestDb.NewContext();
        var tac = await TestData.AddBrandAsync(context, "TAÇ", "tac");
        var karaca = await TestData.AddBrandAsync(context, "Karaca", "karaca");

        var a = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 900m, stock: 5);
        await TestData.SetBrandAsync(context, a, tac);
        await TestData.AddAttributeAsync(context, a, "Malzeme", "Çelik", 0);
        await TestData.AddAttributeAsync(context, a, "Renk", "Gri", 1);

        var b = await TestData.AddHomeProductAsync(context, "Döküm Tava", "dokum-tava", price: 700m, stock: 0);
        await TestData.SetBrandAsync(context, b, karaca);
        await TestData.AddAttributeAsync(context, b, "Malzeme", "Döküm", 0);
        await TestData.AddAttributeAsync(context, b, "Renk", "Kırmızı", 1);

        var c = await TestData.AddHomeProductAsync(context, "Döküm Güveç", "dokum-guvec", price: 1200m, campaignPrice: 990m);
        await TestData.SetBrandAsync(context, c, tac);
        await TestData.AddAttributeAsync(context, c, "Malzeme", "Döküm", 0);
        await TestData.AddAttributeAsync(context, c, "Renk", "Kırmızı", 1);

        var d = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi", price: 300m, campaignPrice: 250m, stock: 3);
        var expired = (await new EfProductDal(context).GetTrackedAsync(p => p.Id == d))!;
        expired.CampaignEndsAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();

        var e = await TestData.AddHomeProductAsync(context, "Keten Örtü", "keten-ortu", price: 400m);
        context.ProductVariants.Add(new ProductVariant { ProductId = e, Color = "Bej", Sku = "KO-BEJ", Stock = 0 });
        await context.SaveChangesAsync();
    }
}
