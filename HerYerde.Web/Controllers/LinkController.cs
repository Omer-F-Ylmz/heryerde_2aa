using HerYerde.Business.Abstract;
using HerYerde.DataAccess.Abstract;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Instagram profil bağlantısı için tek sütunlu giriş sayfası (/link). Dizine eklenmez: aynı içerik
/// ana sayfada zaten var, arama sonucunda ikisi yarışmasın.</summary>
public class LinkController(IProductService productService, IConfiguration configuration) : Controller
{
    /// <summary>Sayfadaki öne çıkan ürün sayısı; mobilde tek ekrana sığsın diye dört.</summary>
    public const int Highlights = 4;

    [HttpGet("link")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (_, hero) = await productService.GetCampaignHeroAsync(now, cancellationToken);

        // Öne çıkanlar; işaretli ürün yoksa en yeniler.
        var (_, featured) = await productService.GetActiveAsync(
            new ProductQuery { FeaturedOnly = true, Order = ProductOrder.Featured, Now = now, Take = Highlights },
            cancellationToken);
        var items = featured.Data!.Items;
        if (items.Count == 0)
        {
            var (_, arrivals) = await productService.GetActiveAsync(
                new ProductQuery { Order = ProductOrder.Newest, Now = now, Take = Highlights },
                cancellationToken);
            items = arrivals.Data!.Items;
        }

        var heroProduct = hero.Data?.Product;
        var ids = items.Select(i => i.Product.Id).ToList();
        if (heroProduct is not null)
        {
            ids.Add(heroProduct.Id);
        }

        var (_, images) = await productService.GetImagesForAsync(ids, cancellationToken);
        string? ImageOf(int productId) => images.Data!
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .FirstOrDefault()?.Url;

        return View(new LinkPageVm(
            heroProduct,
            heroProduct is null ? null : ImageOf(heroProduct.Id),
            items.Select(i => new LinkProductVm(
                i.Product.Name,
                "/urun/" + i.Product.Slug,
                ImageOf(i.Product.Id),
                StoreCatalog.PlaceholderIcon(i.CategorySlug),
                Money.Tl(StoreCatalog.EffectivePrice(i.Product, now)),
                i.SoldOut)).ToList(),
            configuration["Shop:WhatsApp"] ?? "https://wa.me/"));
    }
}
