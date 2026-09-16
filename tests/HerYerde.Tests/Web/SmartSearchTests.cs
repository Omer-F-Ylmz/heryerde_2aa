using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D15 B4: ek toleransı, kelime sırası, eş anlamlı sözlük (docs/arama-esanlam.md), /ara/oner önerileri,
/// arama günlüğü (IP yok) ve yönetim arama raporu.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SmartSearchTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Cogul_ekli_ve_kelime_sirasi_farkli_arama_urunu_bulur()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Döküm Tava", "dokum-tava");
            await TestData.AddHomeProductAsync(context, "Döküm Güveç", "dokum-guvec");
        }

        Assert.Contains("Döküm Tava", await GetAsync("/ara?q=tavalar"));

        var reversed = await GetAsync("/ara?q=tava%20d%C3%B6k%C3%BCm");
        Assert.Contains("Döküm Tava", reversed);
        Assert.DoesNotContain("Döküm Güveç", reversed);
    }

    [Fact]
    public async Task Es_anlamli_sozlukteki_kelime_urunu_bulur()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Cam Karaf", "cam-karaf");
        }

        Assert.Contains("Cam Karaf", await GetAsync("/ara?q=s%C3%BCrahi"));
    }

    [Fact]
    public async Task Oneri_ucu_marka_kategori_ve_urunleri_en_cok_sekiz_satirda_doner()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddBrandAsync(context, "Tava Dünyası", "tava-dunyasi");
            await TestData.AddChildCategoryAsync(context, "Tencere & Tava", "tencere-tava");
            for (var i = 1; i <= 10; i++)
            {
                await TestData.AddHomeProductAsync(context, $"Granit Tava {i}", $"granit-tava-{i}");
            }
        }

        var response = await _factory.CreateNonRedirectingClient().GetAsync("/ara/oner?q=tava");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("<html", html);
        Assert.Equal(8, Regex.Matches(html, "class=\"suggest__item\"").Count);
        Assert.Contains("href=\"/marka/tava-dunyasi\"", html);
        Assert.Contains("href=\"/ev/tencere-tava\"", html);
        Assert.Matches("href=\"/urun/granit-tava-[0-9]+\"", html);
    }

    [Fact]
    public async Task Oneri_ucu_kisa_terimde_bos_doner()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/ara/oner?q=t");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Arama_gunlugune_yalniz_ilk_sayfa_sonuc_sayisiyla_yazilir_ip_telefon_eposta_tutulmaz()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Granit Tava", "granit-tava");
        }

        await GetAsync("/ara?q=zzzzq");
        await GetAsync("/ara?q=Granit%20%20TAVA");
        await GetAsync("/ara?q=granit&sayfa=2");
        await GetAsync("/ara?q=granit&sirala=fiyat");
        await GetAsync("/ara?q=granit&stok=1");
        await GetAsync("/ara?q=granit&sayim=1");
        await GetAsync("/ara/oner?q=granit");
        // Kutuya yazılmış telefon ya da e-posta kişisel veridir: günlüğe hiç girmez.
        await GetAsync("/ara?q=0532%20123%2045%2067");
        await GetAsync("/ara?q=ayse%40ornek.com");

        await using var check = TestDb.NewContext();
        var logs = await check.SearchLogs.OrderBy(l => l.Id).ToListAsync();
        Assert.Equal([("zzzzq", 0), ("granit tava", 1)], logs.Select(l => (l.Term, l.ResultCount)));
        Assert.Equal(["Id", "Term", "ResultCount", "CreatedAt"], typeof(SearchLog).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task Yonetim_arama_raporu_sonucsuz_ve_en_cok_aranan_terimleri_sayilariyla_gosterir()
    {
        await using (var context = TestDb.NewContext())
        {
            var now = TestClock.Now;
            context.SearchLogs.AddRange(
                new SearchLog { Term = "kazan", ResultCount = 0, CreatedAt = now.AddDays(-1) },
                new SearchLog { Term = "kazan", ResultCount = 0, CreatedAt = now.AddDays(-2) },
                new SearchLog { Term = "kazan", ResultCount = 0, CreatedAt = now.AddDays(-3) },
                new SearchLog { Term = "tava", ResultCount = 5, CreatedAt = now.AddDays(-1) },
                new SearchLog { Term = "tava", ResultCount = 5, CreatedAt = now.AddDays(-1) },
                new SearchLog { Term = "tava", ResultCount = 5, CreatedAt = now.AddDays(-1) },
                new SearchLog { Term = "tava", ResultCount = 5, CreatedAt = now.AddDays(-1) },
                new SearchLog { Term = "eski", ResultCount = 0, CreatedAt = now.AddDays(-40) });
            await context.SaveChangesAsync();
        }

        var anonymous = await _factory.CreateNonRedirectingClient().GetAsync("/admin/arama");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);

        var client = await _factory.CreateSignedInClientAsync();
        var html = await (await client.GetAsync("/admin/arama")).Content.ReadAsStringAsync();

        var zero = Section(html, "sonucsuz");
        Assert.Matches("<th scope=\"row\">kazan</th>\\s*<td class=\"table__num\">3</td>", zero);
        Assert.DoesNotContain("tava", zero);
        Assert.DoesNotContain("<th scope=\"row\">eski</th>", html);
        Assert.Matches("<th scope=\"row\">tava</th>\\s*<td class=\"table__num\">4</td>", Section(html, "en-cok"));
    }

    private static string Section(string html, string id)
    {
        var start = html.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, id + " bölümü yok");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private async Task<string> GetAsync(string url)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
