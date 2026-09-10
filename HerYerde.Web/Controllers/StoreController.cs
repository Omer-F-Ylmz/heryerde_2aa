using HerYerde.Business.Abstract;
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
        var (_, items) = await productService.GetActiveWithCategorySlugAsync(cancellationToken);
        var categorySlugOf = items.Data!.ToDictionary(i => i.Product.Id, i => i.CategorySlug);
        var products = items.Data!.Select(i => i.Product).ToList();
        var hero = StoreCatalog.PickHero(products, now);
        var arrivals = StoreCatalog.Sort(products, "yeni", now).Take(8).ToList();

        var ids = arrivals.Select(p => p.Id).ToList();
        if (hero is not null)
        {
            ids.Add(hero.Id);
        }

        var (_, images) = await productService.GetImagesForAsync(ids, cancellationToken);
        var heroImage = hero is null ? null : images.Data!.Where(i => i.ProductId == hero.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).FirstOrDefault()?.Url;

        return View(new HomeVm(
            hero,
            heroImage,
            hero is null ? null : StoreCatalog.PlaceholderIcon(categorySlugOf.GetValueOrDefault(hero.Id)),
            hero is null ? null : StoreCatalog.WhatsAppUrl(WhatsAppBase, hero.Name),
            arrivals.Select((p, i) => StoreCatalog.Card(p, images.Data!, now, lazy: i >= 4, categorySlug: categorySlugOf.GetValueOrDefault(p.Id))).ToList(),
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

        var scope = current is null ? children.Select(c => c.Id).ToHashSet() : [current.Id];
        var (_, all) = await productService.GetAllAsync(cancellationToken);
        var sorted = StoreCatalog.Sort(all.Data!.Where(p => p.IsActive && scope.Contains(p.CategoryId)), sirala, now).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(sorted.Count / (double)StoreCatalog.PageSize));
        sayfa = Math.Clamp(sayfa, 1, totalPages);
        var page = sorted.Skip((sayfa - 1) * StoreCatalog.PageSize).Take(StoreCatalog.PageSize).ToList();
        var (_, images) = await productService.GetImagesForAsync(page.Select(p => p.Id).ToList(), cancellationToken);

        var baseUrl = current is null ? "/ev" : "/ev/" + current.Slug;
        var tabs = new List<CategoryTabVm> { new("Tümü", "/ev", current is null) };
        tabs.AddRange(children.Select(c => new CategoryTabVm(c.Name, "/ev/" + c.Slug, c.Id == current?.Id)));

        return View(new CategoryPageVm(
            current?.Name ?? root.Name,
            tabs,
            sirala == "fiyat" ? "fiyat" : "yeni",
            baseUrl,
            page.Select((p, i) => StoreCatalog.Card(p, images.Data!, now, lazy: i >= 4, categorySlug: children.First(c => c.Id == p.CategoryId).Slug)).ToList(),
            new PaginationVm(sayfa, totalPages, sirala == "fiyat" ? baseUrl + "?sirala=fiyat" : baseUrl)));
    }

    [HttpGet("urun/{slug}")]
    public async Task<IActionResult> Product(string slug, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (_, all) = await productService.GetAllAsync(cancellationToken);
        var product = all.Data!.FirstOrDefault(p => p.Slug == slug && p.IsActive);
        if (product is null)
        {
            return NotFound();
        }

        var (_, categories) = await categoryService.GetAllAsync(cancellationToken);
        var category = categories.Data!.FirstOrDefault(c => c.Id == product.CategoryId);
        var root = category?.ParentId is { } parentId ? categories.Data!.FirstOrDefault(c => c.Id == parentId) : category;
        var isClothing = root?.Slug == "giyim";
        var (rootName, rootUrl) = StoreCatalog.Root(root?.Slug, root?.Name ?? "Ev");

        var (_, images) = await productService.GetImagesAsync(product.Id, cancellationToken);
        var (_, variants) = await productService.GetVariantsAsync(product.Id, cancellationToken);
        var similar = StoreCatalog.Similar(all.Data!, product);
        var (_, similarImages) = await productService.GetImagesForAsync(similar.Select(p => p.Id).ToList(), cancellationToken);

        return View(new ProductPageVm(
            product,
            rootName,
            rootUrl,
            // Ürünün kategorisi kökün kendisiyse kırıntı ikinci kez yazılmaz.
            category is null || category.Id == root?.Id ? null : category.Name,
            category?.Slug,
            isClothing,
            images.Data!.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).ToList(),
            isClothing ? StoreCatalog.Picker(variants.Data!) : new VariantPickerVm([], []),
            StoreCatalog.IsCampaignActive(product, now),
            StoreCatalog.WhatsAppUrl(WhatsAppBase, product.Name),
            similar.Select(p => StoreCatalog.Card(p, similarImages.Data!, now, categorySlug: category?.Slug)).ToList()));
    }
}
