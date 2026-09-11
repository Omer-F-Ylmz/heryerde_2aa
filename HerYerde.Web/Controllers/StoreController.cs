using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Vitrin: ana sayfa, /ev kategorileri, /ara, /urun/{slug}.</summary>
public class StoreController(IProductService productService, ICategoryService categoryService, IConfiguration configuration) : Controller
{
    private string WhatsAppBase => configuration["Shop:WhatsApp"] ?? "https://wa.me/";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (_, heroItem) = await productService.GetCampaignHeroAsync(now, cancellationToken);
        var (_, arrivals) = await productService.GetActiveAsync(
            new ProductQuery { Order = ProductOrder.Newest, Now = now, Take = 8 },
            cancellationToken);

        var hero = heroItem.Data?.Product;
        var ids = arrivals.Data!.Items.Select(i => i.Product.Id).ToList();
        if (hero is not null)
        {
            ids.Add(hero.Id);
        }

        var (_, images) = await productService.GetImagesForAsync(ids, cancellationToken);
        var heroImage = hero is null ? null : images.Data!.Where(i => i.ProductId == hero.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).FirstOrDefault()?.Url;

        return View(new HomeVm(
            hero,
            heroImage,
            hero is null ? null : StoreCatalog.PlaceholderIcon(heroItem.Data!.CategorySlug),
            hero is null ? null : StoreCatalog.WhatsAppUrl(WhatsAppBase, hero.Name),
            Cards(arrivals.Data!.Items, images.Data!, now),
            TestimonialSource.Load()));
    }

    [HttpGet("ev")]
    [HttpGet("ev/{slug}")]
    public async Task<IActionResult> Category(
        string? slug,
        string? sirala,
        decimal? min,
        decimal? max,
        int sayfa = 1,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var (_, categories) = await categoryService.GetAllAsync(cancellationToken);
        var root = categories.Data!.FirstOrDefault(c => c.Slug == "ev" && c.ParentId is null && c.IsActive);
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
                return NotFound();
            }
        }

        var scope = current is null ? children.Select(c => c.Id).ToList() : [current.Id];
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

        var baseUrl = current is null ? "/ev" : "/ev/" + current.Slug;
        var tabs = new List<CategoryTabVm> { new("Tümü", "/ev", current is null) };
        tabs.AddRange(children.Select(c => new CategoryTabVm(c.Name, "/ev/" + c.Slug, c.Id == current?.Id)));

        return View(new CategoryPageVm(
            current?.Name ?? root.Name,
            tabs,
            Sort(baseUrl, byPrice, term: null, low, high),
            Filter(baseUrl, byPrice, term: null, low, high),
            Cards(page, images.Data!, now),
            new PaginationVm(clamped, TotalPages(total), StoreCatalog.Url(
                baseUrl,
                ("sirala", byPrice ? "fiyat" : null),
                ("min", StoreCatalog.Amount(low)),
                ("max", StoreCatalog.Amount(high)))),
            total));
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

    [HttpGet("urun/{slug}")]
    public async Task<IActionResult> Product(string slug, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (status, found) = await productService.GetActiveBySlugAsync(slug, cancellationToken);
        if (status != System.Net.HttpStatusCode.OK)
        {
            return NotFound();
        }

        var product = found.Data!;
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

        // Ürünün ve benzerlerinin görselleri tek sorguda.
        var (_, images) = await productService.GetImagesForAsync(
            similar.Data!.Items.Select(i => i.Product.Id).Append(product.Id).ToList(),
            cancellationToken);

        // Varyant seçici yalnız giyimde çizilir; ev ürününde sorgu da atılmaz.
        var variants = isClothing
            ? (await productService.GetVariantsAsync(product.Id, cancellationToken)).Item2.Data!
            : [];

        // Giyimde tüm varyantlar bittiyse, Evde ürünün kendi stoğu sıfırsa tükendi.
        var soldOut = isClothing
            ? variants.Count > 0 && variants.All(v => v.Stock == 0)
            : product.Stock == 0;

        return View(new ProductPageVm(
            product,
            rootName,
            rootUrl,
            // Ürünün kategorisi kökün kendisiyse kırıntı ikinci kez yazılmaz.
            category is null || category.Id == root?.Id ? null : category.Name,
            category?.Slug,
            isClothing,
            soldOut,
            images.Data!.Where(i => i.ProductId == product.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ToList(),
            isClothing ? StoreCatalog.Picker(variants) : new VariantPickerVm([], []),
            StoreCatalog.IsCampaignActive(product, now),
            StoreCatalog.WhatsAppUrl(WhatsAppBase, product.Name),
            Cards(similar.Data!.Items, images.Data!, now)));
    }

    private static int TotalPages(int total)
        => Math.Max(1, (int)Math.Ceiling(total / (double)StoreCatalog.PageSize));

    private static List<ProductCardVm> Cards(IReadOnlyList<ProductListItem> items, List<ProductImage> images, DateTime now)
        => items
            .Select((i, index) => StoreCatalog.Card(i.Product, images, now, lazy: index >= 4, categorySlug: i.CategorySlug, soldOut: i.SoldOut))
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
