using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerSharp;

namespace HerYerde.Tests.Web;

/// <summary>Gerçek tarayıcıda ölçülen davranışlar: 390px ödeme taşması, JSON-LD'nin CSP ile uyumu.</summary>
[Collection(DatabaseCollection.Name)]
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
}
