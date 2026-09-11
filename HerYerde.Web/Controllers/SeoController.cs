using System.Globalization;
using System.Text;
using System.Xml.Linq;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Abstract;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Controllers;

/// <summary>Arama motorları için /sitemap.xml ve /robots.txt.</summary>
public class SeoController(
    IProductService productService,
    ICategoryService categoryService,
    IOptions<ShopSettings> shop,
    TimeProvider clock) : Controller
{
    /// <summary>Kişiye özel ya da içeriği ince sayfalar taranmaz.</summary>
    private static readonly string[] Closed = ["/admin", "/sepet", "/odeme", "/siparis", "/ara"];

    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [HttpGet("sitemap.xml")]
    [OutputCache(Duration = 3600)]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var (_, products) = await productService.GetActiveAsync(new ProductQuery { Now = now }, cancellationToken);
        var (_, categories) = await categoryService.GetAllAsync(cancellationToken);
        var items = products.Data!.Items;

        // Listeleme sayfasının son değişikliği, içindeki en son güncellenen ürün.
        DateTime Latest(Func<ProductListItem, bool> inScope)
            => items.Where(inScope).Select(i => i.Product.UpdatedAt).DefaultIfEmpty(now).Max();

        var urls = new List<(string Path, DateTime LastModified)> { ("/", Latest(_ => true)) };
        var root = categories.Data!.FirstOrDefault(c => c.Slug == "ev" && c.ParentId is null && c.IsActive);
        if (root is not null)
        {
            var children = categories.Data!.Where(c => c.ParentId == root.Id && c.IsActive).ToList();
            var childIds = children.Select(c => c.Id).ToHashSet();
            urls.Add(("/ev", Latest(i => i.Product.CategoryId == root.Id || childIds.Contains(i.Product.CategoryId))));
            urls.AddRange(children.Select(c => ("/ev/" + c.Slug, Latest(i => i.Product.CategoryId == c.Id))));
        }

        urls.AddRange(items.Select(i => ("/urun/" + i.Product.Slug, i.Product.UpdatedAt)));

        var urlset = new XElement(Ns + "urlset", urls.Select(u => new XElement(Ns + "url",
            new XElement(Ns + "loc", Seo.Absolute(shop.Value.BaseUrl, u.Path)),
            new XElement(Ns + "lastmod", u.LastModified.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))));

        return Content("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + urlset, "application/xml", Encoding.UTF8);
    }

    [HttpGet("robots.txt")]
    public IActionResult Robots()
    {
        var lines = new List<string> { "User-agent: *" };
        lines.AddRange(Closed.Select(path => "Disallow: " + path));
        lines.Add("Sitemap: " + Seo.Absolute(shop.Value.BaseUrl, "/sitemap.xml"));
        return Content(string.Join('\n', lines) + "\n", "text/plain", Encoding.UTF8);
    }
}
