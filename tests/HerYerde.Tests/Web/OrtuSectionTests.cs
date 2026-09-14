using System.Net;
using System.Text.RegularExpressions;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Infrastructure;

namespace HerYerde.Tests.Web;

/// <summary>D8: Örtü &amp; Eşarp bölümü. Veritabanındaki kök slug "giyim" kalır; vitrin yolu /ortu.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrtuSectionTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Ortu_koku_200_doner_alt_kategori_sekmeleriyle()
    {
        await using var context = TestDb.NewContext();
        await OrtuCategoryAsync(context, "Eşarp", "esarp");
        await OrtuCategoryAsync(context, "Şal", "sal");

        var html = await GetHtmlAsync("/ortu");

        Assert.Contains("Örtü &amp; Eşarp", html);
        Assert.Contains("href=\"/ortu/esarp\"", html);
        Assert.Contains("href=\"/ortu/sal\"", html);
    }

    [Fact]
    public async Task Ortu_alt_kategorisi_200_doner_bilinmeyen_alt_kategori_404()
    {
        await using var context = TestDb.NewContext();
        await OrtuCategoryAsync(context, "Eşarp", "esarp");

        var html = await GetHtmlAsync("/ortu/esarp");

        Assert.Matches("<h1[^>]*>Eşarp</h1>", html);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateNonRedirectingClient().GetAsync("/ortu/yok-boyle")).StatusCode);
    }

    [Fact]
    public async Task Bos_kategori_markali_bos_durum_ve_ev_baglantisi_gosterir()
    {
        await using var context = TestDb.NewContext();
        await OrtuCategoryAsync(context, "Namaz Örtüsü", "namaz-ortusu");

        var html = await GetHtmlAsync("/ortu/namaz-ortusu");

        var empty = Regex.Match(html, "<div class=\"empty[^\"]*\"[^>]*>.*?</div>", RegexOptions.Singleline).Value;
        Assert.Contains("Şu an ürün yok", empty);
        Assert.Contains("href=\"/ev\"", empty);
        Assert.DoesNotContain("yakında", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Menude_ortu_linki_tiklanir_yakinda_etiketi_yoktur()
    {
        await using var context = TestDb.NewContext();
        await OrtuCategoryAsync(context, "Eşarp", "esarp");

        var html = await GetHtmlAsync("/");

        var nav = Regex.Match(html, "<nav class=\"store-nav\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.Matches("<a class=\"store-nav__link\" href=\"/ortu\">Örtü &amp; Eşarp</a>", nav);
        Assert.DoesNotContain("store-nav__link--soon", nav);
        Assert.DoesNotContain("yakında", nav);
        Assert.Matches("<a class=\"door door--active\" href=\"/ortu\">", html);
        Assert.DoesNotContain("door--soon", html);
    }

    [Fact]
    public async Task Giyim_urununun_kiritisi_ortu_kokune_ve_alt_kategoriye_baglanir()
    {
        await using var context = TestDb.NewContext();
        var esarp = await OrtuCategoryAsync(context, "Eşarp", "esarp");
        await ColorOnlyProductAsync(context, esarp, "İpek eşarp", "ipek-esarp");

        var html = await GetHtmlAsync("/urun/ipek-esarp");

        var crumbs = Regex.Match(html, "<nav class=\"crumbs\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.Contains("href=\"/ortu\"", crumbs);
        Assert.Contains("href=\"/ortu/esarp\"", crumbs);
        // JSON-LD kırıntısı da aynı kökü taşır.
        Assert.Contains($"\"item\":\"{AdminWebFactory.BaseUrl}/ortu/esarp\"", html);
    }

    [Fact]
    public async Task Sitemap_ortu_kokunu_ve_alt_kategorilerini_kapsar()
    {
        await using var context = TestDb.NewContext();
        await OrtuCategoryAsync(context, "Eşarp", "esarp");
        await OrtuCategoryAsync(context, "Bone & Aksesuar", "bone-aksesuar");

        var xml = await GetHtmlAsync("/sitemap.xml");

        Assert.Contains($"<loc>{AdminWebFactory.BaseUrl}/ortu</loc>", xml);
        Assert.Contains($"<loc>{AdminWebFactory.BaseUrl}/ortu/esarp</loc>", xml);
        Assert.Contains($"<loc>{AdminWebFactory.BaseUrl}/ortu/bone-aksesuar</loc>", xml);
    }

    [Fact]
    public async Task Ortu_alt_kategorisindeki_urun_varyantsiz_yayina_alinamaz()
    {
        await using var context = TestDb.NewContext();
        var esarp = await OrtuCategoryAsync(context, "Eşarp", "esarp");
        var manager = TestData.NewProductManager(context);
        var (_, created) = await manager.AddAsync(new Product
        {
            Name = "Pamuk eşarp",
            Description = "Yazlık.",
            CategoryId = esarp,
            Price = 350m,
            IsActive = false
        });

        var product = created.Data!;
        product.IsActive = true;
        var (status, result) = await manager.UpdateAsync(product);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("varyant", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bedensiz_urunde_secici_yalniz_renk_ciplerini_tek_satirda_cizer()
    {
        await using var context = TestDb.NewContext();
        var esarp = await OrtuCategoryAsync(context, "Eşarp", "esarp");
        await ColorOnlyProductAsync(context, esarp, "İpek eşarp", "ipek-esarp");

        var html = await GetHtmlAsync("/urun/ipek-esarp");

        Assert.DoesNotContain(">Beden</legend>", html);
        Assert.Matches("<fieldset class=\"chips chips--row\">\\s*<legend class=\"chips__legend\">Renk / desen</legend>", html);
        Assert.Contains("id=\"color-bordo\"", html);
        Assert.Contains("id=\"color-lacivert-desenli\"", html);
        Assert.Contains("Sepete eklemek için renk seçin.", html);
    }

    [Fact]
    public async Task Beden_ve_renkli_urunde_iki_secici_birlikte_cizilir()
    {
        await using var context = TestDb.NewContext();
        var esarp = await OrtuCategoryAsync(context, "Bone & Aksesuar", "bone-aksesuar");
        var productId = await TestData.AddProductAsync(context, esarp, "Pamuk bone", "pamuk-bone");
        var variants = new EfProductVariantDal(context);
        await variants.AddAsync(new ProductVariant { ProductId = productId, Size = "S", Color = "Siyah", Sku = "BONE-S-SIYAH", Stock = 2 });
        await variants.AddAsync(new ProductVariant { ProductId = productId, Size = "M", Color = "Siyah", Sku = "BONE-M-SIYAH", Stock = 2 });
        await new EfUnitOfWork(context).SaveChangesAsync();

        var html = await GetHtmlAsync("/urun/pamuk-bone");

        Assert.Contains(">Beden</legend>", html);
        Assert.Contains(">Renk</legend>", html);
        Assert.Contains("id=\"size-m\"", html);
        Assert.Contains("id=\"color-siyah\"", html);
        Assert.Contains("Sepete eklemek için beden ve renk seçin.", html);
    }

    [Fact]
    public async Task Olcu_urun_detayinda_satir_olarak_gorunur_kartta_gorunmez()
    {
        await using var context = TestDb.NewContext();
        var esarp = await OrtuCategoryAsync(context, "Eşarp", "esarp");
        var productId = await ColorOnlyProductAsync(context, esarp, "İpek eşarp", "ipek-esarp");
        var tracked = (await new EfProductDal(context).GetTrackedAsync(p => p.Id == productId))!;
        tracked.Dimensions = "70x70 cm";
        await new EfUnitOfWork(context).SaveChangesAsync();

        var detail = await GetHtmlAsync("/urun/ipek-esarp");
        var listing = await GetHtmlAsync("/ortu/esarp");

        Assert.Matches("<dt[^>]*>Ölçü</dt>\\s*<dd[^>]*>70x70 cm</dd>", detail);
        Assert.Contains("ipek-esarp", listing);
        Assert.DoesNotContain("70x70", listing);
    }

    [Fact]
    public async Task Arama_ve_fiyat_suzgeci_ortu_urunlerini_kapsar()
    {
        await using var context = TestDb.NewContext();
        var esarp = await OrtuCategoryAsync(context, "Eşarp", "esarp");
        await ColorOnlyProductAsync(context, esarp, "İpek eşarp", "ipek-esarp", price: 600m);
        await ColorOnlyProductAsync(context, esarp, "Pamuk eşarp", "pamuk-esarp", price: 200m);

        var search = await GetHtmlAsync("/ara?q=" + Uri.EscapeDataString("eşarp"));
        var filtered = await GetHtmlAsync("/ortu?min=500&max=700");

        Assert.Contains("href=\"/urun/ipek-esarp\"", search);
        Assert.Contains("href=\"/urun/ipek-esarp\"", filtered);
        Assert.DoesNotContain("href=\"/urun/pamuk-esarp\"", filtered);
    }

    [Fact]
    public async Task Ana_sayfa_yeni_gelenler_ev_ve_ortu_urunlerini_karma_listeler()
    {
        await using var context = TestDb.NewContext();
        var esarp = await OrtuCategoryAsync(context, "Eşarp", "esarp");
        await TestData.AddHomeProductAsync(context, "Çelik tencere", "celik-tencere");
        await ColorOnlyProductAsync(context, esarp, "İpek eşarp", "ipek-esarp");

        var html = await GetHtmlAsync("/");

        var arrivals = Regex.Match(html, "<section[^>]*id=\"yeni\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains("href=\"/urun/celik-tencere\"", arrivals);
        Assert.Contains("href=\"/urun/ipek-esarp\"", arrivals);
    }

    [Fact]
    public async Task Yonetici_urun_formunda_olcu_girer_ve_kaydedilir()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddChildCategoryAsync(context, "Sepet & Dekor", "sepet-dekor");
        var client = await _factory.CreateSignedInClientAsync();

        var form = await (await client.GetAsync("/admin/products/create")).Content.ReadAsStringAsync();
        var response = await HtmlForm.PostAsync(client, "/admin/products/create", "/admin/products/create", new Dictionary<string, string>
        {
            ["Name"] = "Hasır sepet",
            ["Description"] = "Örgü.",
            ["CategoryId"] = categoryId.ToString(),
            ["Price"] = "250.00",
            ["Dimensions"] = "30x20 cm",
            ["IsActive"] = "false"
        });

        Assert.Contains("name=\"Dimensions\"", form);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("30x20 cm", (await new EfProductDal(context).GetAsync(p => p.Slug == "hasir-sepet"))!.Dimensions);
    }

    [Fact]
    public async Task Seed_ev_dali_doluyken_de_ortu_alt_kategorilerini_bir_kez_ekler()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddChildCategoryAsync(context, "Tencere & Tava", "tencere-tava");

        await DataSeeder.SeedCatalogAsync(context);
        await using var second = TestDb.NewContext();
        await DataSeeder.SeedCatalogAsync(second);

        var giyim = await new EfCategoryDal(second).GetAsync(c => c.Slug == "giyim" && c.ParentId == null);
        Assert.NotNull(giyim);
        var names = (await new EfCategoryDal(second).GetListAsync(c => c.ParentId == giyim.Id)).OrderBy(c => c.SortOrder).Select(c => c.Name);
        Assert.Equal(["Eşarp", "Başörtüsü", "Şal", "Namaz Örtüsü", "Bone & Aksesuar"], names);
    }

    [Fact]
    public void Ithal_komutu_tablo_dosyasini_argumandan_alir_yoksa_ithal_1()
    {
        Assert.Equal("docs/ithal-2.md", ImportCommand.TableFrom(["--ithal", "brand_assets/raw", "--tablo", "docs/ithal-2.md"]));
        Assert.Equal(Path.Combine("docs", "ithal-1.md"), ImportCommand.TableFrom(["--ithal", "brand_assets/raw"]));
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Giyim kökünün (slug "giyim") altında alt kategori; kök yoksa açılır.</summary>
    private static async Task<int> OrtuCategoryAsync(HerYerdeContext context, string name, string slug)
    {
        var dal = new EfCategoryDal(context);
        var root = await dal.GetAsync(c => c.Slug == "giyim");
        var rootId = root?.Id ?? await TestData.AddRootCategoryAsync(context, "Giyim", "giyim");
        var category = new Category { Name = name, Slug = slug, ParentId = rootId, SortOrder = 1, IsActive = true };
        await dal.AddAsync(category);
        await new EfUnitOfWork(context).SaveChangesAsync();
        return category.Id;
    }

    /// <summary>Bedensiz, iki renkli (biri desenli) yayında eşarp.</summary>
    private static async Task<int> ColorOnlyProductAsync(HerYerdeContext context, int categoryId, string name, string slug, decimal price = 450m)
    {
        var product = TestData.NewProduct(categoryId, name, slug);
        product.Price = price;
        await new EfProductDal(context).AddAsync(product);
        await new EfUnitOfWork(context).SaveChangesAsync();

        var variants = new EfProductVariantDal(context);
        await variants.AddAsync(new ProductVariant { ProductId = product.Id, Color = "Bordo", Sku = slug.ToUpperInvariant() + "-BORDO", Stock = 4 });
        await variants.AddAsync(new ProductVariant { ProductId = product.Id, Color = "Lacivert desenli", Sku = slug.ToUpperInvariant() + "-LACIVERT", Stock = 2 });
        await new EfUnitOfWork(context).SaveChangesAsync();
        return product.Id;
    }
}
