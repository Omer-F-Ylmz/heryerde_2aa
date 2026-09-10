using HerYerde.Business.Abstract;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Vitrin: ana sayfa, /ev kategorileri, /urun/{slug}.</summary>
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
            arrivals.Data!.Items.Select((i, index) => StoreCatalog.Card(i.Product, images.Data!, now, lazy: index >= 4, categorySlug: i.CategorySlug)).ToList(),
            TestimonialSource.Load()));
    }

    [HttpGet("ev")]
    [HttpGet("ev/{slug}")]
    public async Task<IActionResult> Category(string? slug, string? sirala, int sayfa = 1, CancellationToken cancellationToken = default)
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
        var (_, first) = await productService.GetActiveAsync(
            new ProductQuery
            {
                CategoryIds = scope,
                Order = byPrice ? ProductOrder.Price : ProductOrder.Newest,
                Now = now,
                Skip = (Math.Max(sayfa, 1) - 1) * StoreCatalog.PageSize,
                Take = StoreCatalog.PageSize
            },
            cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(first.Data!.Total / (double)StoreCatalog.PageSize));
        var clamped = Math.Clamp(sayfa, 1, totalPages);
        var page = first.Data.Items;
        if (clamped != sayfa)
        {
            // İstenen sayfa aralık dışıysa sınıra çekilir; ikinci sorgu yalnız bu durumda çalışır.
            var (_, retry) = await productService.GetActiveAsync(
                new ProductQuery
                {
                    CategoryIds = scope,
                    Order = byPrice ? ProductOrder.Price : ProductOrder.Newest,
                    Now = now,
                    Skip = (clamped - 1) * StoreCatalog.PageSize,
                    Take = StoreCatalog.PageSize
                },
                cancellationToken);
            page = retry.Data!.Items;
        }

        var (_, images) = await productService.GetImagesForAsync(page.Select(i => i.Product.Id).ToList(), cancellationToken);

        var baseUrl = current is null ? "/ev" : "/ev/" + current.Slug;
        var tabs = new List<CategoryTabVm> { new("Tümü", "/ev", current is null) };
        tabs.AddRange(children.Select(c => new CategoryTabVm(c.Name, "/ev/" + c.Slug, c.Id == current?.Id)));

        return View(new CategoryPageVm(
            current?.Name ?? root.Name,
            tabs,
            byPrice ? "fiyat" : "yeni",
            baseUrl,
            page.Select((i, index) => StoreCatalog.Card(i.Product, images.Data!, now, lazy: index >= 4, categorySlug: i.CategorySlug)).ToList(),
            new PaginationVm(clamped, totalPages, byPrice ? baseUrl + "?sirala=fiyat" : baseUrl)));
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

        return View(new ProductPageVm(
            product,
            rootName,
            rootUrl,
            // Ürünün kategorisi kökün kendisiyse kırıntı ikinci kez yazılmaz.
            category is null || category.Id == root?.Id ? null : category.Name,
            category?.Slug,
            isClothing,
            images.Data!.Where(i => i.ProductId == product.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ToList(),
            isClothing ? StoreCatalog.Picker(variants) : new VariantPickerVm([], []),
            StoreCatalog.IsCampaignActive(product, now),
            StoreCatalog.WhatsAppUrl(WhatsAppBase, product.Name),
            similar.Data!.Items.Select(i => StoreCatalog.Card(i.Product, images.Data!, now, categorySlug: i.CategorySlug)).ToList()));
    }
}
