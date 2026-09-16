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

    /// <summary>YAYIN-KAPI: arama kutusu öneri listesi kapalıyken aria-expanded taşımaz (searchbox rolünde geçersiz öznitelik),
    /// liste açılınca "true" olur, Esc ile kapanınca yine kalkar.</summary>
    [Fact]
    public async Task Arama_kutusu_oneri_kapaliyken_aria_expanded_tasimaz_acikken_true()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        await using var page = await _browser.NewPageAsync();
        await page.GoToAsync(_baseUrl + "/", WaitUntilNavigation.Networkidle0);
        const string expanded = "(() => { const i = document.querySelector('#site-search'); return i.hasAttribute('aria-expanded') ? i.getAttribute('aria-expanded') : 'yok'; })()";

        var closed = await page.EvaluateExpressionAsync<string>(expanded);
        await page.TypeAsync("#site-search", "tence");
        await page.WaitForSelectorAsync("#search-suggest:not([hidden]) .suggest__item");
        var open = await page.EvaluateExpressionAsync<string>(expanded);
        await page.Keyboard.PressAsync("Escape");
        var closedAgain = await page.EvaluateExpressionAsync<string>(expanded);

        Assert.Equal("yok", closed);
        Assert.Equal("true", open);
        Assert.Equal("yok", closedAgain);
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

    /// <summary>D15 A1: videolu ürünün kartında üzerine gelince sessiz önizleme kurulur; azaltılmış hareket tercihinde
    /// oynatıcı hiç oluşmaz.</summary>
    [Fact]
    public async Task Kart_onizlemesi_ustune_gelince_kurulur_azaltilmis_harekette_kurulmaz()
    {
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            await TestData.AddVideoAsync(context, productId);
        }

        const string preview = "!!document.querySelector('.card__media .card__preview')";

        await using var motion = await _browser.NewPageAsync();
        await motion.SetViewportAsync(new ViewPortOptions { Width = 1440, Height = 900 });
        // Başsız Chrome varsayılan olarak "reduce" bildiriyor: iki tercih de açıkça verilir.
        await motion.EmulateMediaFeaturesAsync([new MediaFeatureValue { MediaFeature = MediaFeature.PrefersReducedMotion, Value = "no-preference" }]);
        await motion.GoToAsync(_baseUrl + "/ev", WaitUntilNavigation.Networkidle0);
        // Gerçek imleç: kartın üstünü başlık bağlantısının ::after katmanı kaplar, olay medyaya değil karta düşer.
        await motion.HoverAsync(".card:has([data-onizleme]) .card__media");
        Assert.True(await motion.EvaluateExpressionAsync<bool>(preview));

        await using var still = await _browser.NewPageAsync();
        await still.SetViewportAsync(new ViewPortOptions { Width = 1440, Height = 900 });
        await still.EmulateMediaFeaturesAsync([new MediaFeatureValue { MediaFeature = MediaFeature.PrefersReducedMotion, Value = "reduce" }]);
        await still.GoToAsync(_baseUrl + "/ev", WaitUntilNavigation.Networkidle0);
        await still.HoverAsync(".card:has([data-onizleme]) .card__media");
        Assert.False(await still.EvaluateExpressionAsync<bool>(preview));
    }

    /// <summary>D15 B3: 390'da süzgeç paneli kapalı başlar ve düğmeyle açılır; 1440'ta her zaman açıktır. Seçenek
    /// işaretlenince sayfa yenilenmeden gönder düğmesi yeni sonuç sayısını yazar.</summary>
    [Fact]
    public async Task Suzgec_paneli_mobilde_katlanir_secimde_sonuc_sayisini_canli_yazar()
    {
        await using (var context = TestDb.NewContext())
        {
            var tac = await TestData.AddBrandAsync(context, "TAÇ", "tac");
            await TestData.SetBrandAsync(context, await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere"), tac);
            await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
            await TestData.AddHomeProductAsync(context, "Keten Örtü", "keten-ortu");
        }

        // Kapalı <details> içeriği content-visibility: hidden; boyut ölçümü yerleşimi zorlar, görünürlük checkVisibility ile sorulur.
        const string optionVisible = "document.querySelector('.filter-option').checkVisibility()";

        await using var phone = await _browser.NewPageAsync();
        await phone.SetViewportAsync(new ViewPortOptions { Width = 390, Height = 900 });
        await phone.GoToAsync(_baseUrl + "/ev", WaitUntilNavigation.Networkidle0);
        Assert.False(await phone.EvaluateExpressionAsync<bool>(optionVisible));
        await phone.ClickAsync(".filter-panel__toggle");
        Assert.True(await phone.EvaluateExpressionAsync<bool>(optionVisible));
        // Açık panel (fiyat alanları dahil) 390'da yatay taşmaz.
        Assert.Equal(390, await phone.EvaluateExpressionAsync<int>("document.documentElement.scrollWidth"));

        const string button = "document.querySelector('[data-filter-count]').textContent.trim()";
        Assert.Equal("3 ürünü göster", await phone.EvaluateExpressionAsync<string>(button));
        var url = phone.Url;
        await phone.ClickAsync("input[name=marka][value=tac]");
        await phone.WaitForExpressionAsync(button + " === '1 ürünü göster'", new WaitForFunctionOptions { Timeout = 5000 });
        Assert.Equal(url, phone.Url);

        await using var desktop = await _browser.NewPageAsync();
        await desktop.SetViewportAsync(new ViewPortOptions { Width = 1440, Height = 900 });
        await desktop.GoToAsync(_baseUrl + "/ev", WaitUntilNavigation.Networkidle0);
        Assert.True(await desktop.EvaluateExpressionAsync<bool>(optionVisible));
        Assert.False(await desktop.EvaluateExpressionAsync<bool>("document.querySelector('.filter-panel__toggle').checkVisibility()"));
    }

    /// <summary>D15 B4: başlıktaki arama kutusuna yazınca öneriler açılır; aşağı ok ilk öneriye, Esc kapatıp kutuya döner.</summary>
    [Fact]
    public async Task Arama_onerileri_yazinca_acilir_klavyeyle_gezilir_esc_kapatir()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Döküm Tava", "dokum-tava");
        }

        await using var page = await _browser.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions { Width = 1440, Height = 900 });
        await page.GoToAsync(_baseUrl + "/", WaitUntilNavigation.Networkidle0);
        await page.TypeAsync("#site-search", "tav");
        await page.WaitForExpressionAsync("document.querySelectorAll('.suggest__item').length > 0", new WaitForFunctionOptions { Timeout = 5000 });

        Assert.Equal("true", await page.EvaluateExpressionAsync<string>("document.querySelector('#site-search').getAttribute('aria-expanded')"));
        Assert.Equal("/urun/dokum-tava", await page.EvaluateExpressionAsync<string>("document.querySelector('.suggest__item').getAttribute('href')"));

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.True(await page.EvaluateExpressionAsync<bool>("document.activeElement.classList.contains('suggest__item')"));

        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("site-search", await page.EvaluateExpressionAsync<string>("document.activeElement.id"));
        Assert.False(await page.EvaluateExpressionAsync<bool>("document.querySelector('.suggest').checkVisibility()"));
        // YAYIN-KAPI: kapalı liste aria-expanded taşımaz.
        Assert.False(await page.EvaluateExpressionAsync<bool>("document.querySelector('#site-search').hasAttribute('aria-expanded')"));
    }

    /// <summary>D16 A2: service worker vitrin sayfalarını ve statik varlıkları önbelleğe alır, sepet/ödeme/yönetimi almaz;
    /// çevrimdışıyken önbellekteki sayfa açılır, olmayan sayfada markalı çevrimdışı sayfası gelir; site kurulabilir (CDP).</summary>
    [Fact]
    public async Task Service_worker_ozel_yollari_onbelleklemez_cevrimdisi_sayfa_gosterir_site_kurulabilir()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        await using var page = await _browser.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions { Width = 390, Height = 900 });
        await page.GoToAsync(_baseUrl + "/", WaitUntilNavigation.Networkidle0);
        await page.EvaluateExpressionAsync("navigator.serviceWorker.ready.then(() => true)");
        await page.WaitForExpressionAsync("navigator.serviceWorker.controller !== null", new WaitForFunctionOptions { Timeout = 10000 });

        foreach (var path in new[] { "/ev", "/urun/celik-tencere", "/sepet", "/odeme", "/admin/auth/login" })
        {
            await page.GoToAsync(_baseUrl + path, WaitUntilNavigation.Networkidle0);
        }

        var cached = await page.EvaluateExpressionAsync<string[]>(
            "(async () => { const out = []; for (const key of await caches.keys()) { const cache = await caches.open(key); for (const r of await cache.keys()) { out.push(new URL(r.url).pathname); } } return out; })()");
        Assert.Contains("/ev", cached);
        Assert.Contains("/urun/celik-tencere", cached);
        Assert.Contains("/cevrimdisi", cached);
        Assert.Contains(cached, p => p.StartsWith("/css/site.css", StringComparison.Ordinal));
        Assert.DoesNotContain(cached, p => p.StartsWith("/sepet", StringComparison.Ordinal) || p.StartsWith("/odeme", StringComparison.Ordinal) || p.StartsWith("/admin", StringComparison.Ordinal));

        var installability = await page.Client.SendAsync<System.Text.Json.JsonElement>("Page.getInstallabilityErrors");
        Assert.Equal(0, installability.GetProperty("installabilityErrors").GetArrayLength());

        // Çevrimdışı öykünmesi sayfa hedefine uygulanır; service worker'ın kendi istekleri için onun hedefi de kapatılır.
        await page.SetOfflineModeAsync(true);
        var worker = _browser.Targets().First(t => t.Type == TargetType.ServiceWorker);
        var workerSession = await worker.CreateCDPSessionAsync();
        await workerSession.SendAsync("Network.enable");
        await workerSession.SendAsync("Network.emulateNetworkConditions", new { offline = true, latency = 0, downloadThroughput = -1, uploadThroughput = -1 });
        await page.GoToAsync(_baseUrl + "/ev", WaitUntilNavigation.Load);
        Assert.Equal("Ev", await page.EvaluateExpressionAsync<string>("document.querySelector('h1').textContent.trim()"));
        await page.GoToAsync(_baseUrl + "/marka/hic-acilmamis", WaitUntilNavigation.Load);
        Assert.Contains("Şu an çevrimdışısınız", await page.EvaluateExpressionAsync<string>("document.body.textContent"));
    }

    /// <summary>D16 A2: "ana ekrana ekle" ipucu ilk ziyarette çıkmaz, ikinci ziyarette çıkar (iOS'ta yönerge); kapatılınca bir yıl
    /// görünmez (localStorage).</summary>
    [Fact]
    public async Task Ana_ekrana_ekle_ipucu_ikinci_ziyarette_cikar_kapatilinca_gorunmez()
    {
        const string IPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";
        const string hint = "document.querySelector('[data-install-hint]').checkVisibility()";

        async Task<IPage> VisitAsync()
        {
            var page = await _browser.NewPageAsync();
            await page.SetUserAgentAsync(IPhone);
            await page.SetViewportAsync(new ViewPortOptions { Width = 390, Height = 900 });
            await page.GoToAsync(_baseUrl + "/", WaitUntilNavigation.Networkidle0);
            return page;
        }

        await using var first = await VisitAsync();
        Assert.False(await first.EvaluateExpressionAsync<bool>(hint));

        await using var second = await VisitAsync();
        Assert.True(await second.EvaluateExpressionAsync<bool>(hint));
        Assert.Contains("Ana Ekrana Ekle", await second.EvaluateExpressionAsync<string>("document.querySelector('[data-install-hint]').textContent"));
        await second.ClickAsync("[data-install-dismiss]");
        Assert.False(await second.EvaluateExpressionAsync<bool>(hint));

        await using var third = await VisitAsync();
        Assert.False(await third.EvaluateExpressionAsync<bool>(hint));
    }
}
