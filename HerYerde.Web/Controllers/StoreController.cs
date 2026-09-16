using HerYerde.Business.Abstract;
using HerYerde.Business.Concrete;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Vitrin: ana sayfa, /ev ve /ortu kategorileri, /ara, /urun/{slug} ve ürün yorumu gönderimi.</summary>
public class StoreController(
    IProductService productService,
    ICategoryService categoryService,
    IReviewService reviewService,
    IInstallmentService installments,
    IConfiguration configuration) : Controller
{
    private const string ReviewNoticeKey = "YorumBildirimi";

    /// <summary>Ana sayfadaki alıntı sayısı.</summary>
    private const int TestimonialCount = 4;

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
        int sayfa = 1,
        CancellationToken cancellationToken = default)
        => ListingAsync("ev", slug, sirala, min, max, sayfa, cancellationToken);

    /// <summary>Örtü &amp; Eşarp: veritabanında "giyim" kökü; görünen ad ve yol vitrine özel.</summary>
    [HttpGet("ortu")]
    [HttpGet("ortu/{slug}")]
    public Task<IActionResult> Ortu(
        string? slug,
        string? sirala,
        decimal? min,
        decimal? max,
        int sayfa = 1,
        CancellationToken cancellationToken = default)
        => ListingAsync("giyim", slug, sirala, min, max, sayfa, cancellationToken);

    private async Task<IActionResult> ListingAsync(
        string rootSlug,
        string? slug,
        string? sirala,
        decimal? min,
        decimal? max,
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

        ProductQuery Query(int page) => new()
        {
            CategoryIds = scope,
            Order = byPrice ? ProductOrder.Price : ProductOrder.Newest,
            MinPrice = low,
            MaxPrice = high,
            Now = now,
            Skip = (page - 1) * StoreCatalog.PageSize,
            Take = StoreCatalog.PageSize
        };

        var (page, clamped, total) = await PageAsync(Query, sayfa, cancellationToken);
        var (_, images) = await productService.GetImagesForAsync(page.Select(i => i.Product.Id).ToList(), cancellationToken);

        var (rootName, rootPath) = StoreCatalog.Root(root.Slug, root.Name);
        var baseUrl = current is null ? rootPath : rootPath + "/" + current.Slug;
        var tabs = new List<CategoryTabVm> { new("Tümü", rootPath, current is null) };
        tabs.AddRange(children.Select(c => new CategoryTabVm(c.Name, rootPath + "/" + c.Slug, c.Id == current?.Id, CategoryImages.Square(c.ImageUrl))));

        return View("Category", new CategoryPageVm(
            current?.Name ?? rootName,
            tabs,
            Sort(baseUrl, byPrice, term: null, low, high),
            Filter(baseUrl, byPrice, term: null, low, high),
            Cards(page, images.Data!, now),
            new PaginationVm(clamped, TotalPages(total), StoreCatalog.Url(
                baseUrl,
                ("sirala", byPrice ? "fiyat" : null),
                ("min", StoreCatalog.Amount(low)),
                ("max", StoreCatalog.Amount(high)))),
            total,
            rootName,
            // Boş rafta öteki kök önerilir: Örtü'de Ev, Ev'de Örtü & Eşarp.
            rootSlug == "giyim" ? new CategoryTabVm("Ev ürünlerine bak", "/ev", false) : new CategoryTabVm("Örtü & Eşarp'a bak", "/ortu", false),
            current?.ImageUrl ?? root.ImageUrl,
            StoreCatalog.PlaceholderIcon(current?.Slug)));
    }

    [HttpGet("ara")]
    public async Task<IActionResult> Search(
        string? q,
        string? sirala,
        decimal? min,
        decimal? max,
        int sayfa = 1,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var term = StoreCatalog.Term(q);
        var byPrice = sirala == "fiyat";
        var (low, high) = StoreCatalog.Range(min, max);
        var sort = Sort("/ara", byPrice, term, low, high);
        var filter = Filter("/ara", byPrice, term, low, high);

        // Kısa terim hata değil: kutu doldurulmuş sayfa mesajla döner, sorgu hiç atılmaz.
        if (term.Length < StoreCatalog.MinTermLength)
        {
            return View(new SearchPageVm(
                term,
                "Aramak için en az 2 harf yazın.",
                sort,
                filter,
                [],
                [],
                0,
                new PaginationVm(1, 1, "/ara")));
        }

        ProductQuery Query(int page) => new()
        {
            Term = term,
            Order = byPrice ? ProductOrder.Price : ProductOrder.Newest,
            MinPrice = low,
            MaxPrice = high,
            Now = now,
            Skip = (page - 1) * StoreCatalog.PageSize,
            Take = StoreCatalog.PageSize
        };

        var (page, clamped, total) = await PageAsync(Query, sayfa, cancellationToken);

        // Sonuç yoksa eli boş dönmesin: son gelen dört ürün önerilir.
        var suggestions = total == 0
            ? (await productService.GetActiveAsync(
                new ProductQuery { Order = ProductOrder.Newest, Now = now, Take = 4 },
                cancellationToken)).Item2.Data!.Items
            : [];

        var ids = page.Select(i => i.Product.Id).Concat(suggestions.Select(i => i.Product.Id)).ToList();
        var (_, images) = await productService.GetImagesForAsync(ids, cancellationToken);

        return View(new SearchPageVm(
            term,
            null,
            sort,
            filter,
            Cards(page, images.Data!, now),
            Cards(suggestions, images.Data!, now),
            total,
            new PaginationVm(clamped, TotalPages(total), StoreCatalog.Url(
                "/ara",
                ("q", term),
                ("sirala", byPrice ? "fiyat" : null),
                ("min", StoreCatalog.Amount(low)),
                ("max", StoreCatalog.Amount(high))))));
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
            highlighted));
    }

    private static int TotalPages(int total)
        => Math.Max(1, (int)Math.Ceiling(total / (double)StoreCatalog.PageSize));

    private static List<ProductCardVm> Cards(IReadOnlyList<ProductListItem> items, List<ProductImage> images, DateTime now)
        => items
            .Select((i, index) => StoreCatalog.Card(i.Product, images, now, lazy: index >= 4, categorySlug: i.CategorySlug, soldOut: i.SoldOut, previewUrl: i.PreviewUrl))
            .ToList();

    private static SortTabsVm Sort(string path, bool byPrice, string? term, decimal? low, decimal? high)
        => new(
            StoreCatalog.Url(path, ("q", term), ("min", StoreCatalog.Amount(low)), ("max", StoreCatalog.Amount(high))),
            StoreCatalog.Url(path, ("q", term), ("sirala", "fiyat"), ("min", StoreCatalog.Amount(low)), ("max", StoreCatalog.Amount(high))),
            byPrice);

    private static PriceFilterVm Filter(string path, bool byPrice, string? term, decimal? low, decimal? high)
    {
        var hidden = new List<HiddenFieldVm>();
        if (!string.IsNullOrEmpty(term))
        {
            hidden.Add(new HiddenFieldVm("q", term));
        }

        if (byPrice)
        {
            hidden.Add(new HiddenFieldVm("sirala", "fiyat"));
        }

        return new PriceFilterVm(path, low, high, hidden);
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
