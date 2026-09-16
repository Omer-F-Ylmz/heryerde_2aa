using System.Globalization;
using System.Text;
using System.Xml.Linq;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Controllers;

/// <summary>Meta katalog ve Google Merchant ürün akışları. İkisi de RSS 2.0 + g: alanları; Meta bu biçimi olduğu
/// gibi okur, Google'da ek olarak kampanyalıda sale_price gider.</summary>
public class FeedController(
    IProductService productService,
    IBrandService brandService,
    IOptions<ShopSettings> shop,
    TimeProvider clock) : Controller
{
    /// <summary>Akışlar gün içinde nadiren değişir; kaynak her indirmede yeniden üretilmez.</summary>
    public const int CacheSeconds = 3600;

    private static readonly XNamespace G = "http://base.google.com/ns/1.0";

    [HttpGet("feeds/meta.xml")]
    [OutputCache(Duration = CacheSeconds)]
    public Task<IActionResult> Meta(CancellationToken cancellationToken)
        => BuildAsync("HerYerde ürün katalogu", withSalePrice: false, cancellationToken);

    [HttpGet("feeds/google.xml")]
    [OutputCache(Duration = CacheSeconds)]
    public Task<IActionResult> Google(CancellationToken cancellationToken)
        => BuildAsync("HerYerde Merchant akışı", withSalePrice: true, cancellationToken);

    private async Task<IActionResult> BuildAsync(string title, bool withSalePrice, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var baseUrl = shop.Value.BaseUrl;
        var (_, page) = await productService.GetActiveAsync(new ProductQuery { Now = now }, cancellationToken);

        // Fiyatı olmayan ürün akışa girmez: Merchant fiyatsız kaydı zaten reddeder.
        var products = page.Data!.Items.Where(i => StoreCatalog.EffectivePrice(i.Product, now) > 0m).ToList();
        var ids = products.Select(i => i.Product.Id).ToList();
        var (_, images) = await productService.GetImagesForAsync(ids, cancellationToken);
        var (_, variants) = await productService.GetVariantsForAsync(ids, cancellationToken);
        var variantsByProduct = variants.Data!.ToLookup(v => v.ProductId);
        var brandNames = (await brandService.GetAllAsync(cancellationToken)).ToDictionary(b => b.Id, b => b.Name);

        var items = new List<XElement>();
        foreach (var listItem in products)
        {
            var product = listItem.Product;
            var price = StoreCatalog.EffectivePrice(product, now);
            var listPrice = product.Price > price ? product.Price : price;
            var image = images.Data!
                .Where(i => i.ProductId == product.Id)
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .FirstOrDefault();

            var category = FeedCatalog.Category(listItem.CategorySlug);
            var own = variantsByProduct[product.Id].OrderBy(v => v.Id).ToList();
            if (own.Count == 0)
            {
                items.Add(Item(product, category, price, listPrice, image, product.Slug, null, product.Stock is not 0, null));
                continue;
            }

            // Varyantlı üründe her beden/renk ayrı kayıt; hepsi item_group_id ile aynı ürüne bağlanır.
            foreach (var variant in own)
            {
                items.Add(Item(product, category, price, listPrice, image, variant.Sku, product.Slug, variant.Stock > 0, variant));
            }
        }

        var channel = new XElement(
            "channel",
            new XElement("title", title),
            new XElement("link", baseUrl),
            new XElement("description", "HerYerde — çeyizden mutfağa ev ürünleri."),
            items);

        var rss = new XElement(
            "rss",
            new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "g", G.NamespaceName),
            channel);

        return Content("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + rss, "application/xml", Encoding.UTF8);

        XElement Item(
            Product product,
            string category,
            decimal price,
            decimal listPrice,
            ProductImage? image,
            string id,
            string? groupId,
            bool inStock,
            ProductVariant? variant)
        {
            var name = variant is null ? product.Name : $"{product.Name} · {VariantLabel(product, variant)}";
            var element = new XElement(
                "item",
                new XElement(G + "id", id),
                new XElement(G + "title", name),
                new XElement(G + "description", Seo.ProductDescription(product, price)),
                new XElement(G + "link", Seo.Absolute(baseUrl, "/urun/" + product.Slug)),
                new XElement(G + "availability", inStock ? "in stock" : "out of stock"),
                new XElement(G + "condition", "new"),
                // Markasız ürün (el yapımı, markasız ithal) mağaza adıyla çıkar: Merchant marka alanını boş kabul etmez.
                new XElement(G + "brand", product.BrandId is { } brandId && brandNames.TryGetValue(brandId, out var brandName) ? brandName : FeedCatalog.Brand),
                new XElement(G + "google_product_category", category));

            if (image is not null)
            {
                element.Add(new XElement(G + "image_link", Seo.Absolute(baseUrl, image.Url)));
            }

            if (groupId is not null)
            {
                element.Add(new XElement(G + "item_group_id", groupId));
            }

            // Google kampanyayı liste fiyatı + sale_price ile ister; Meta yalnız geçerli fiyatı okur.
            if (withSalePrice && listPrice > price)
            {
                element.Add(new XElement(G + "price", Amount(listPrice)));
                element.Add(new XElement(G + "sale_price", Amount(price)));
            }
            else
            {
                element.Add(new XElement(G + "price", Amount(price)));
            }

            return element;
        }
    }

    /// <summary>"M / Kiremit"; eksen adı ürüne göre değişse de akışta beden-renk sırası korunur.</summary>
    private static string VariantLabel(Product product, ProductVariant variant)
    {
        var parts = new[] { variant.Size, variant.Color }.Where(p => !string.IsNullOrWhiteSpace(p));
        var label = string.Join(" / ", parts);
        return label.Length > 0 ? label : product.Slug;
    }

    /// <summary>"1290.90 TRY" — Merchant nokta ondalıklı, iki haneli ve para birimi ekli ister.</summary>
    private static string Amount(decimal value)
        => value.ToString("0.00", CultureInfo.InvariantCulture) + " TRY";
}
