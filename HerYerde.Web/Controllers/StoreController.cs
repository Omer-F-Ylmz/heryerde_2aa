using HerYerde.Business.Abstract;
using HerYerde.Business.Concrete;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Vitrin: ana sayfa, /ev ve /ortu kategorileri, /marka/{slug}, /ara ve /ara/oner, /urun/{slug} ve ürün yorumu gönderimi.</summary>
public class StoreController(
    IProductService productService,
    ICategoryService categoryService,
    IBrandService brandService,
    IReviewService reviewService,
    IInstallmentService installments,
    ISearchLogService searchLogs,
    IConfiguration configuration) : Controller
{
    private const string ReviewNoticeKey = "YorumBildirimi";

    /// <summary>Ana sayfadaki alıntı sayısı.</summary>
    private const int TestimonialCount = 4;

    /// <summary>Arama önerisi: toplam satır ve marka/kategori için ayrılan en çok satır.</summary>
    private const int SuggestionCount = 8;
    private const int SuggestionGroupCount = 2;

    private string WhatsAppBase => configuration["Shop:WhatsApp"] ?? "https://wa.me/";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (_, heroItem) = await productService.GetCampaignHeroAsync(now, cancellationToken);
        var (_, arrivals) = await productService.GetActiveAsync(
            new ProductQuery { Order = ProductOrder.Newest, Now = now, Take = 8 },
            cancellationToken);

        // Öne çıkanlar rafı; işaretli yayındaki ürün yoksa yeni gelenlere düşer.
        var (_, featured) = await productService.GetActiveAsync(
            new ProductQuery { FeaturedOnly = true, Order = ProductOrder.Featured, Now = now, Take = 8 },
            cancellationToken);
        var shelf = featured.Data!.Items.Count > 0 ? featured.Data!.Items : arrivals.Data!.Items;

        var hero = heroItem.Data?.Product;
        var ids = shelf.Select(i => i.Product.Id).ToList();
        if (hero is not null)
        {
            ids.Add(hero.Id);
        }

        var (_, images) = await productService.GetImagesForAsync(ids, cancellationToken);
        var heroImage = hero is null ? null : images.Data!.Where(i => i.ProductId == hero.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).FirstOrDefault()?.Url;

        // Onaylı yorum varsa gerçek yorumlar, yoksa sabit DM alıntıları.
        var (_, reviews) = await reviewService.GetLatestApprovedAsync(TestimonialCount, cancellationToken);
        IReadOnlyList<TestimonialVm> testimonials = reviews.Data!.Count > 0
            ? reviews.Data!.Select(r => new TestimonialVm(r.Review.Name, r.Review.Comment, r.ProductName + " yorumu")).ToList()
            : TestimonialSource.Load();

        // Kapı kartlarının görseli kök kategoriden.
        var (_, categories) = await categoryService.GetAllAsync(cancellationToken);
        string? RootImage(string rootSlug) => categories.Data!.FirstOrDefault(c => c.Slug == rootSlug && c.ParentId is null)?.ImageUrl;

        return View(new HomeVm(
            hero,
            heroImage,
            hero is null ? null : StoreCatalog.PlaceholderIcon(heroItem.Data!.CategorySlug),
            hero is null ? null : StoreCatalog.WhatsAppUrl(WhatsAppBase, hero.Name),
            Cards(shelf, images.Data!, now),
            featured.Data!.Items.Count > 0,
            testimonials,
            RootImage("ev"),
            RootImage("giyim")));
    }

    [HttpGet("ev")]
    [HttpGet("ev/{slug}")]
    public Task<IActionResult> Category(
        string? slug,
        string? sirala,
        decimal? min,
        decimal? max,
        ListingSelection secim,
        int sayfa = 1,
        CancellationToken cancellationToken = default)
        => ListingAsync("ev", slug, sirala, min, max, secim, sayfa, cancellationToken);

    /// <summary>Örtü &amp; Eşarp: veritabanında "giyim" kökü; görünen ad ve yol vitrine özel.</summary>
    [HttpGet("ortu")]
    [HttpGet("ortu/{slug}")]
    public Task<IActionResult> Ortu(
        string? slug,
        string? sirala,
        decimal? min,
        decimal? max,
        ListingSelection secim,
        int sayfa = 1,
        CancellationToken cancellationToken = default)
        => ListingAsync("giyim", slug, sirala, min, max, secim, sayfa, cancellationToken);

    private async Task<IActionResult> ListingAsync(
        string rootSlug,
        string? slug,
        string? sirala,
        decimal? min,
        decimal? max,
        ListingSelection secim,
        int sayfa,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (_, categories) = await categoryService.GetAllAsync(cancellationToken);
        var root = categories.Data!.FirstOrDefault(c => c.Slug == rootSlug && c.ParentId is null && c.IsActive);
        if (root is null)
        {
            return NotFound();
        }

        var children = categories.Data!.Where(c => c.ParentId == root.Id && c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
        Category? current = null;
        if (slug is not null)
        {
            current = children.FirstOrDefault(c => c.Slug == slug);
            if (current is null)
            {
                var (moved, renamed) = await categoryService.GetByOldSlugAsync(slug, cancellationToken);
                return moved == System.Net.HttpStatusCode.OK && children.Any(c => c.Id == renamed.Data!.Id)
                    ? RedirectPermanent(StoreCatalog.Root(root.Slug, root.Name).Url + "/" + renamed.Data!.Slug + Request.QueryString)
                    : NotFound();
            }
        }

        // Kök de kapsama girer: alt kategorisi olmayan kökte boş liste "süzgeç yok" sayılıp tüm ürünleri getirirdi.
        var scope = current is null ? children.Select(c => c.Id).Append(root.Id).ToList() : [current.Id];
        var byPrice = sirala == "fiyat";
        var (low, high) = StoreCatalog.Range(min, max);

        ProductQuery Query(int page, int take) => new()
        {
            CategoryIds = scope,
            Order = byPrice ? ProductOrder.Price : ProductOrder.Newest,
            MinPrice = low,
            MaxPrice = high,
            Now = now,
            Skip = (page - 1) * take,
            Take = take,
            BrandSlugs = secim.BrandSlugs,
            Attributes = secim.AttributeFilters,
            InStockOnly = secim.InStockOnly,
            CampaignOnly = secim.CampaignOnly
        };

        if (secim.CountOnly)
        {
            return await CountAsync(Query(1, 1), cancellationToken);
        }

        var (page, clamped, total) = await PageAsync(p => Query(p, StoreCatalog.PageSize), sayfa, cancellationToken);
        var (_, images) = await productService.GetImagesForAsync(page.Select(i => i.Product.Id).ToList(), cancellationToken);
        var facets = await productService.GetFacetsAsync(Query(1, StoreCatalog.PageSize), cancellationToken);

        var (rootName, rootPath) = StoreCatalog.Root(root.Slug, root.Name);
        var baseUrl = current is null ? rootPath : rootPath + "/" + current.Slug;
        var tabs = new List<CategoryTabVm> { new("Tümü", rootPath, current is null) };
        tabs.AddRange(children.Select(c => new CategoryTabVm(c.Name, rootPath + "/" + c.Slug, c.Id == current?.Id, CategoryImages.Square(c.ImageUrl))));
        var state = new ListingState(baseUrl, null, byPrice, low, high, secim);

        return View("Category", new CategoryPageVm(
            current?.Name ?? rootName,
            tabs,
            Sort(state),
            Panel(state, facets, total, withBrands: true),
            Cards(page, images.Data!, now),
            new PaginationVm(clamped, TotalPages(total), state.Url()),
            total,
            rootName,
            // Boş rafta öteki kök önerilir: Örtü'de Ev, Ev'de Örtü & Eşarp.
            rootSlug == "giyim" ? new CategoryTabVm("Ev ürünlerine bak", "/ev", false) : new CategoryTabVm("Örtü & Eşarp'a bak", "/ortu", false),
            current?.ImageUrl ?? root.ImageUrl,
            StoreCatalog.PlaceholderIcon(current?.Slug)));
    }

    [HttpGet("marka/{slug}")]
    public async Task<IActionResult> Brand(
        string slug,
        string? sirala,
        decimal? min,
        decimal? max,
        ListingSelection secim,
        int sayfa = 1,
        CancellationToken cancellationToken = default)
    {
        var brand = await brandService.GetBySlugAsync(slug, cancellationToken);
        if (brand is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var byPrice = sirala == "fiyat";
        var (low, high) = StoreCatalog.Range(min, max);
        var path = "/marka/" + brand.Slug;

        ProductQuery Query(int page, int take) => new()
        {
            BrandIds = [brand.Id],
            Order = byPrice ? ProductOrder.Price : ProductOrder.Newest,
            MinPrice = low,
            MaxPrice = high,
            Now = now,
            Skip = (page - 1) * take,
            Take = take,
            BrandSlugs = secim.BrandSlugs,
            Attributes = secim.AttributeFilters,
            InStockOnly = secim.InStockOnly,
            CampaignOnly = secim.CampaignOnly
        };

        if (secim.CountOnly)
        {
            return await CountAsync(Query(1, 1), cancellationToken);
        }

        var (page, clamped, total) = await PageAsync(p => Query(p, StoreCatalog.PageSize), sayfa, cancellationToken);
        var (_, images) = await productService.GetImagesForAsync(page.Select(i => i.Product.Id).ToList(), cancellationToken);
        var facets = await productService.GetFacetsAsync(Query(1, StoreCatalog.PageSize), cancellationToken);
        var state = new ListingState(path, null, byPrice, low, high, secim);

        return View("Category", new CategoryPageVm(
            brand.Name,
            [new CategoryTabVm("Tümü", path, true)],
            Sort(state),
            // Marka sayfasında marka grubu tek seçenekli olurdu: çizilmez.
            Panel(state, facets, total, withBrands: false),
            Cards(page, images.Data!, now),
            new PaginationVm(clamped, TotalPages(total), state.Url()),
            total,
            "Marka",
            new CategoryTabVm("Tüm ürünlere bak", "/ev", false),
            LogoUrl: brand.LogoUrl));
    }

    [HttpGet("ara")]
    public async Task<IActionResult> Search(
        string? q,
        string? sirala,
        decimal? min,
        decimal? max,
        ListingSelection secim,
        int sayfa = 1,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var term = StoreCatalog.Term(q);
        var byPrice = sirala == "fiyat";
        var (low, high) = StoreCatalog.Range(min, max);
        var state = new ListingState("/ara", term, byPrice, low, high, secim);

        // Kısa terim hata değil: kutu doldurulmuş sayfa mesajla döner, sorgu hiç atılmaz.
        if (term.Length < StoreCatalog.MinTermLength)
        {
            return secim.CountOnly
                ? Content("0", "text/plain")
                : View(new SearchPageVm(
                    term,
                    "Aramak için en az 2 harf yazın.",
                    Sort(state),
                    null,
                    [],
                    [],
                    0,
                    new PaginationVm(1, 1, "/ara")));
        }

        var words = SearchTerms.Expand(term, SynonymSource.Load());

        ProductQuery Query(int page, int take) => new()
        {
            SearchWords = words,
            Order = byPrice ? ProductOrder.Price : ProductOrder.Newest,
            MinPrice = low,
            MaxPrice = high,
            Now = now,
            Skip = (page - 1) * take,
            Take = take,
            BrandSlugs = secim.BrandSlugs,
            Attributes = secim.AttributeFilters,
            InStockOnly = secim.InStockOnly,
            CampaignOnly = secim.CampaignOnly
        };

        if (secim.CountOnly)
        {
            return await CountAsync(Query(1, 1), cancellationToken);
        }

        var (page, clamped, total) = await PageAsync(p => Query(p, StoreCatalog.PageSize), sayfa, cancellationToken);
        var facets = await productService.GetFacetsAsync(Query(1, StoreCatalog.PageSize), cancellationToken);

        // Günlüğe yalnız aramanın kendisi: sayfa, sıralama, fiyat ve süzgeç değişimleri aynı aramanın tekrarı sayılmaz.
        if (sayfa <= 1 && !byPrice && low is null && high is null && secim.ActiveCount == 0)
        {
            await searchLogs.RecordAsync(term, total, cancellationToken);
        }

        // Sonuç yoksa eli boş dönmesin: son gelen dört ürün önerilir (süzgeç seçiliyken öneri yok: boşluğu süzgeç yaratmıştır).
        var suggestions = total == 0 && secim.ActiveCount == 0
            ? (await productService.GetActiveAsync(
                new ProductQuery { Order = ProductOrder.Newest, Now = now, Take = 4 },
                cancellationToken)).Item2.Data!.Items
            : [];

        var ids = page.Select(i => i.Product.Id).Concat(suggestions.Select(i => i.Product.Id)).ToList();
        var (_, images) = await productService.GetImagesForAsync(ids, cancellationToken);

        return View(new SearchPageVm(
            term,
            null,
            Sort(state),
            Panel(state, facets, total, withBrands: true),
            Cards(page, images.Data!, now),
            Cards(suggestions, images.Data!, now),
            total,
            new PaginationVm(clamped, TotalPages(total), state.Url())));
    }

    /// <summary>Başlıktaki arama kutusunun önerileri (HTML parçası): ürünler, sonra en çok ikişer kategori ve marka; toplam en çok
    /// sekiz satır. Kısa terimde boş gövde. Günlüğe yazılmaz.</summary>
    [HttpGet("ara/oner")]
    public async Task<IActionResult> Suggest(string? q, CancellationToken cancellationToken)
    {
        var term = StoreCatalog.Term(q);
        if (term.Length < StoreCatalog.MinTermLength)
        {
            return Content(string.Empty, "text/html");
        }

        var words = SearchTerms.Expand(term, SynonymSource.Load());
        var turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        bool Matches(string name)
        {
            var lower = name.ToLower(turkish);
            return words.All(word => word.Any(option => lower.Contains(option, StringComparison.Ordinal)));
        }

        var (_, categories) = await categoryService.GetAllAsync(cancellationToken);
        var active = categories.Data!.Where(c => c.IsActive).ToList();
        var categoryItems = active
            .Select(c => (Category: c, Root: c.ParentId is { } parentId ? active.FirstOrDefault(r => r.Id == parentId) : c))
            .Where(x => x.Root is { ParentId: null })
            .Select(x => (x.Category, Path: StoreCatalog.Root(x.Root!.Slug, x.Root.Name)))
            // Yalnız vitrinde gezilebilen kökler (Ev, Örtü & Eşarp) ve altları.
            .Where(x => x.Path.Url != "/")
            .Select(x => x.Category.ParentId is null
                ? new SuggestionVm(x.Path.Name, x.Path.Url, "Kategori")
                : new SuggestionVm(x.Category.Name, x.Path.Url + "/" + x.Category.Slug, "Kategori"))
            .Where(s => Matches(s.Name))
            .Take(SuggestionGroupCount);
        var brandItems = (await brandService.GetAllAsync(cancellationToken))
            .Where(b => Matches(b.Name))
            .Take(SuggestionGroupCount)
            .Select(b => new SuggestionVm(b.Name, "/marka/" + b.Slug, "Marka"));
        var fixedItems = categoryItems.Concat(brandItems).ToList();

        var now = DateTime.UtcNow;
        var (_, products) = await productService.GetActiveAsync(
            new ProductQuery { SearchWords = words, Now = now, Take = SuggestionCount - fixedItems.Count },
            cancellationToken);
        var items = products.Data!.Items
            .Select(i => new SuggestionVm(i.Product.Name, "/urun/" + i.Product.Slug, Money.Tl(StoreCatalog.EffectivePrice(i.Product, now))))
            .Concat(fixedItems)
            .ToList();

        return PartialView("Components/_SearchSuggest", items);
    }

    /// <summary>siparis: değerlendirme davetinden gelen sipariş numarası; yorum formuna yazılır, doğrulanmış
    /// alıcı rozetini ReviewManager sipariş gerçekten bu ürünü içeriyorsa verir.</summary>
    [HttpGet("urun/{slug}")]
    public async Task<IActionResult> Product(string slug, [FromQuery(Name = "siparis")] string? siparis, CancellationToken cancellationToken)
    {
        var (status, found) = await productService.GetActiveBySlugAsync(slug, cancellationToken);
        if (status != System.Net.HttpStatusCode.OK)
        {
            // Adı değişmiş ürünün eski adresi kalıcı olarak yenisine gider (paylaşılmış bağlantı, arama motoru).
            var (moved, current) = await productService.GetCurrentSlugAsync(slug, cancellationToken);
            return moved == System.Net.HttpStatusCode.OK ? RedirectPermanent("/urun/" + current.Data) : NotFound();
        }

        var form = new ReviewFormViewModel
        {
            OrderNo = siparis is { Length: > 0 and <= 20 } ? siparis : null
        };
        return await ProductViewAsync(found.Data!, form, TempData[ReviewNoticeKey] as string, cancellationToken);
    }

    /// <summary>Yorum onaysız kaydedilir; başarıda 303 ile ürün sayfasına bildirimle dönülür, hatalı formda sayfa 400 ile
    /// alan hatalarını gösterir.</summary>
    [HttpPost("urun/{slug}/yorum")]
    public async Task<IActionResult> Review(string slug, ReviewFormViewModel form, CancellationToken cancellationToken)
    {
        var (status, found) = await productService.GetActiveBySlugAsync(slug, cancellationToken);
        if (status != System.Net.HttpStatusCode.OK)
        {
            return NotFound();
        }

        var product = found.Data!.Product;
        if (ModelState.IsValid)
        {
            var (added, result) = await reviewService.AddAsync(new ProductReview
            {
                ProductId = product.Id,
                Name = form.Name.Trim(),
                Rating = form.Rating,
                Comment = form.Comment.Trim(),
                OrderNo = form.OrderNo
            }, cancellationToken);

            if (added == System.Net.HttpStatusCode.Created)
            {
                TempData[ReviewNoticeKey] = result.Message;
                Response.Headers.Location = $"/urun/{product.Slug}#yorumlar";
                return StatusCode(StatusCodes.Status303SeeOther);
            }

            ModelState.AddModelError(nameof(form.Rating), result.Message);
        }

        Response.StatusCode = StatusCodes.Status400BadRequest;
        return await ProductViewAsync(found.Data!, form, null, cancellationToken);
    }

    private async Task<IActionResult> ProductViewAsync(
        ProductDetail detail,
        ReviewFormViewModel reviewForm,
        string? reviewNotice,
        CancellationToken cancellationToken)
    {
        var product = detail.Product;
        var now = DateTime.UtcNow;
        var (_, categories) = await categoryService.GetAllAsync(cancellationToken);
        var category = categories.Data!.FirstOrDefault(c => c.Id == product.CategoryId);
        var root = category?.ParentId is { } parentId ? categories.Data!.FirstOrDefault(c => c.Id == parentId) : category;
        var isClothing = root?.Slug == "giyim";
        var (rootName, rootUrl) = StoreCatalog.Root(root?.Slug, root?.Name ?? "Ev");

        var (_, similar) = await productService.GetActiveAsync(
            new ProductQuery
            {
                CategoryIds = [product.CategoryId],
                ExcludedProductId = product.Id,
                Now = now,
                Take = 4
            },
            cancellationToken);

        // Ürünün ve benzerlerinin görselleri tek sorguda; benzerlerin önizlemesi listeleme sorgusundan gelir.
        var (_, images) = await productService.GetImagesForAsync(
            similar.Data!.Items.Select(i => i.Product.Id).Append(product.Id).ToList(),
            cancellationToken);
        var (_, videos) = await productService.GetVideosAsync(product.Id, cancellationToken);

        // Varyant her iki alanda da olabilir (D10): varyantlıda stok varyantta, varyantsızda ürünün kendi stoğunda.
        var variants = detail.Variants;
        var soldOut = variants.Count > 0
            ? variants.All(v => v.Stock == 0)
            : product.Stock == 0;

        var (_, reviews) = await reviewService.GetApprovedAsync(product.Id, cancellationToken);

        // Genel taksit tablosu (BIN yok): ürünün etkin fiyatı üzerinden, yalnız öne çıkan taksitler.
        var price = StoreCatalog.EffectivePrice(product, now);
        var table = await installments.GetAsync(price, cancellationToken: cancellationToken);
        var highlighted = table is null || !table.HasInstallments
            ? null
            : new InstallmentTableVm(
                table.Options.Where(o => InstallmentManager.Highlighted.Contains(o.Count)).ToList(),
                null,
                "Kartınızın bankasına göre değişebilir; kesin tablo ödeme adımında görünür.");

        // Marka yalnız atanmışsa sorulur: markasız üründe ek sorgu yok.
        var brand = product.BrandId is { } brandId ? await brandService.GetByIdAsync(brandId, cancellationToken) : null;

        return View("Product", new ProductPageVm(
            product,
            rootName,
            rootUrl,
            // Ürünün kategorisi kökün kendisiyse kırıntı ikinci kez yazılmaz.
            category is null || category.Id == root?.Id ? null : category.Name,
            category?.Slug,
            isClothing,
            soldOut,
            images.Data!.Where(i => i.ProductId == product.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ToList(),
            videos.Data!.OrderBy(v => v.SortOrder).FirstOrDefault(),
            variants.Count > 0 ? StoreCatalog.Picker(variants, product) : new VariantPickerVm([], []),
            StoreCatalog.IsCampaignActive(product, now),
            StoreCatalog.WhatsAppUrl(WhatsAppBase, product.Name),
            Cards(similar.Data!.Items, images.Data!, now),
            new ReviewSectionVm(product.Slug, reviews.Data!, RatingSummary.From(reviews.Data!), reviewForm, reviewNotice),
            highlighted,
            detail.Attributes ?? [],
            brand));
    }

    private static int TotalPages(int total)
        => Math.Max(1, (int)Math.Ceiling(total / (double)StoreCatalog.PageSize));

    private static List<ProductCardVm> Cards(IReadOnlyList<ProductListItem> items, List<ProductImage> images, DateTime now)
        => items
            .Select((i, index) => StoreCatalog.Card(i.Product, images, now, lazy: index >= 4, categorySlug: i.CategorySlug, soldOut: i.SoldOut, previewUrl: i.PreviewUrl))
            .ToList();

    private static SortTabsVm Sort(ListingState state)
        => new(state.Url(byPrice: false), state.Url(byPrice: true), state.ByPrice);

    /// <summary>Canlı sayaç: sayfa satırı yerine tek satırlık sorgunun toplamı, düz metin.</summary>
    private async Task<IActionResult> CountAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var (_, result) = await productService.GetActiveAsync(query, cancellationToken);
        return Content(result.Data!.Total.ToString(System.Globalization.CultureInfo.InvariantCulture), "text/plain");
    }

    private static FilterPanelVm Panel(ListingState state, ListingFacets facets, int total, bool withBrands)
    {
        var selection = state.Selection;
        var groups = new List<FilterGroupVm>();
        if (withBrands && facets.Brands.Count > 0)
        {
            groups.Add(new FilterGroupVm("Marka", facets.Brands
                .Select(b => new FilterOptionVm("marka", b.Slug, b.Name, b.Count, selection.BrandSlugs.Contains(b.Slug)))
                .ToList()));
        }

        groups.AddRange(facets.Attributes.Select(a => new FilterGroupVm(a.Name, a.Values
            .Select(v => new FilterOptionVm("oz", a.Name + ":" + v.Value, v.Value, v.Count, selection.HasAttribute(a.Name, v.Value)))
            .ToList())));
        groups.Add(new FilterGroupVm("Durum",
        [
            new FilterOptionVm("stok", "1", "Stokta olanlar", null, selection.InStockOnly),
            new FilterOptionVm("kampanya", "1", "Kampanyalı", null, selection.CampaignOnly)
        ]));

        var chips = new List<FilterChipVm>();
        if (state.Min is not null || state.Max is not null)
        {
            var range = $"{StoreCatalog.Amount(state.Min) ?? "0"} – {StoreCatalog.Amount(state.Max) ?? "…"} ₺";
            chips.Add(new FilterChipVm(range, state.Url(drop: ("fiyat", null))));
        }

        chips.AddRange(selection.BrandSlugs.Select(slug => new FilterChipVm(
            facets.Brands.FirstOrDefault(b => b.Slug == slug)?.Name ?? slug,
            state.Url(drop: ("marka", slug)))));
        chips.AddRange(selection.AttributePairs.Select(p => new FilterChipVm(
            p.Name + ": " + p.Value,
            state.Url(drop: ("oz", p.Name + ":" + p.Value)))));
        if (selection.InStockOnly)
        {
            chips.Add(new FilterChipVm("Stokta olanlar", state.Url(drop: ("stok", null))));
        }

        if (selection.CampaignOnly)
        {
            chips.Add(new FilterChipVm("Kampanyalı", state.Url(drop: ("kampanya", null))));
        }

        var hidden = new List<HiddenFieldVm>();
        if (!string.IsNullOrEmpty(state.Term))
        {
            hidden.Add(new HiddenFieldVm("q", state.Term));
        }

        if (state.ByPrice)
        {
            hidden.Add(new HiddenFieldVm("sirala", "fiyat"));
        }

        return new FilterPanelVm(state.Path, hidden, state.Min, state.Max, groups, chips, state.Url(onlySort: true), total);
    }

    /// <summary>İstenen sayfa aralık dışıysa sınıra çekilir; ikinci sorgu yalnız bu durumda çalışır.</summary>
    private async Task<(List<ProductListItem> Items, int Page, int Total)> PageAsync(
        Func<int, ProductQuery> query,
        int wanted,
        CancellationToken cancellationToken)
    {
        var requested = Math.Max(wanted, 1);
        var (_, first) = await productService.GetActiveAsync(query(requested), cancellationToken);
        var total = first.Data!.Total;
        var clamped = Math.Clamp(requested, 1, TotalPages(total));
        if (clamped == requested)
        {
            return (first.Data.Items, clamped, total);
        }

        var (_, retry) = await productService.GetActiveAsync(query(clamped), cancellationToken);
        return (retry.Data!.Items, clamped, total);
    }
}
