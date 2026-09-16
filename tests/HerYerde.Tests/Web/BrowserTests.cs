using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerSharp;

namespace HerYerde.Tests.Web;

/// <summary>Gerçek tarayıcıda ölçülen davranışlar: 390px ödeme taşması, JSON-LD'nin CSP ile uyumu.
/// Chrome indirdiği için build-test'te süzülür; CI'da browser-check job'u haftalık ve elle koşar.</summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Browser")]
public sealed class BrowserTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();
    private IBrowser _browser = null!;
    private string _baseUrl = string.Empty;

    public async Task InitializeAsync()
    {
        await TestDb.ResetAsync();
        _factory.UseKestrel(0);
        _factory.StartServer();
        _baseUrl = _factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        await new BrowserFetcher().DownloadAsync();
        _browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = ["--no-sandbox"] });
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _factory.Dispose();
    }

    [Fact]
    public async Task Odeme_sayfasi_390_genislikte_yatay_tasmaz()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        await using var page = await _browser.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions { Width = 390, Height = 900 });
        await page.GoToAsync(_baseUrl + "/urun/celik-tencere", WaitUntilNavigation.Networkidle0);
        await Task.WhenAll(page.WaitForNavigationAsync(), page.ClickAsync("[data-add-button]"));
        await page.GoToAsync(_baseUrl + "/odeme", WaitUntilNavigation.Networkidle0);

        Assert.EndsWith("/odeme", page.Url);
        Assert.Equal(390, await page.EvaluateExpressionAsync<int>("document.documentElement.scrollWidth"));

        await page.ClickAsync("label[for=pay-havale]");
        Assert.Equal(390, await page.EvaluateExpressionAsync<int>("document.documentElement.scrollWidth"));
    }

    /// <summary>D10 A3: varyantlı üründe "Son N adet" rozeti seçili varyantın stoğunu izler.</summary>
    [Fact]
    public async Task Varyant_secince_son_adet_rozeti_degisir()
    {
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Keten Örtü", "keten-ortu");
            context.ProductVariants.AddRange(
                new HerYerde.Entities.Concrete.ProductVariant { ProductId = productId, Color = "Bej", Sku = "KO-BEJ", Stock = 2 },
                new HerYerde.Entities.Concrete.ProductVariant { ProductId = productId, Color = "Gri", Sku = "KO-GRI", Stock = 9 });
            await context.SaveChangesAsync();
        }

        await using var page = await _browser.NewPageAsync();
        await page.GoToAsync(_baseUrl + "/urun/keten-ortu", WaitUntilNavigation.Networkidle0);
        const string badge = "(() => { const b = document.querySelector('[data-low-stock]'); return b && !b.hidden ? b.textContent.trim() : ''; })()";

        var before = await page.EvaluateExpressionAsync<string>(badge);
        await page.ClickAsync("label[for=color-bej]");
        var low = await page.EvaluateExpressionAsync<string>(badge);
        await page.ClickAsync("label[for=color-gri]");
        var plenty = await page.EvaluateExpressionAsync<string>(badge);

        Assert.Equal("", before);
        Assert.Equal("Son 2 adet", low);
        Assert.Equal("", plenty);
    }

    [Fact]
    public async Task Urun_sayfasindaki_json_ld_ayrisir_ve_script_csp_ihlali_uretmez()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        await using var page = await _browser.NewPageAsync();
        await page.EvaluateExpressionOnNewDocumentAsync(
            "window.__csp = []; document.addEventListener('securitypolicyviolation', e => window.__csp.push(e.violatedDirective));");
        await page.GoToAsync(_baseUrl + "/urun/celik-tencere", WaitUntilNavigation.Networkidle0);

        var types = await page.EvaluateExpressionAsync<string[]>(
            "[...document.querySelectorAll('script[type=\"application/ld+json\"]')].map(s => JSON.parse(s.textContent)['@type'])");
        var scriptViolations = await page.EvaluateExpressionAsync<string[]>(
            "window.__csp.filter(d => d.startsWith('script-src'))");

        Assert.Contains("Product", types);
        Assert.Contains("BreadcrumbList", types);
        Assert.Empty(scriptViolations);
    }

    /// <summary>D14 A2: "İlçeleri getir" formdaki ilk submit düğmesi olduğu için formun varsayılan düğmesidir;
    /// JS varken devre dışı bırakılmazsa alanda Enter'a basmak siparişi göndermek yerine ilçe listesini tazeler.</summary>
    [Fact]
    public async Task Adres_alaninda_enter_siparis_formunu_gonderir_ilce_tazelemez()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        await using var page = await _browser.NewPageAsync();
        await page.GoToAsync(_baseUrl + "/urun/celik-tencere", WaitUntilNavigation.Networkidle0);
        await Task.WhenAll(page.WaitForNavigationAsync(), page.ClickAsync("[data-add-button]"));
        await page.GoToAsync(_baseUrl + "/odeme", WaitUntilNavigation.Networkidle0);

        // JS açıkken tazeleme düğmesi gizli VE devre dışı olmalı: devre dışı düğme varsayılan submit sayılmaz.
        Assert.True(await page.EvaluateExpressionAsync<bool>("document.querySelector('[data-district-refresh]').hidden"));
        Assert.True(await page.EvaluateExpressionAsync<bool>("document.querySelector('[data-district-refresh]').disabled"));

        // Tarayıcının seçtiği varsayılan düğme sipariş düğmesi olmalı.
        var defaultButton = await page.EvaluateExpressionAsync<string>(
            "[...document.querySelector('.checkout__form').elements].filter(e => e.type === 'submit' && !e.disabled)[0].textContent.trim()");
        Assert.Equal("Siparişi tamamla", defaultButton);
    }
}
