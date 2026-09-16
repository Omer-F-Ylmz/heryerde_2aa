using HerYerde.Business.Abstract;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Çerezsiz analitik raporu (D16): günlük görüntülenme/ziyaret, en çok görüntülenen ürün ve kategori, kaynaklar,
/// huni ve WhatsApp/Instagram tıklamaları. Varsayılan son 30 gün; aralık en çok bir yıl.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class AnalyticsController(
    IAnalyticsService analytics,
    IProductService products,
    ICategoryService categories,
    TimeProvider clock) : Controller
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;

    [HttpGet("admin/analitik")]
    public async Task<IActionResult> Index(DateOnly? baslangic, DateOnly? bitis, CancellationToken cancellationToken)
    {
        var to = bitis ?? IstanbulTime.Today(clock);
        var from = baslangic ?? to.AddDays(-(DefaultDays - 1));
        if (from > to)
        {
            (from, to) = (to, from);
        }

        if (to.DayNumber - from.DayNumber >= MaxDays)
        {
            from = to.AddDays(-(MaxDays - 1));
        }

        var report = await analytics.GetReportAsync(from, to, cancellationToken);

        // Yol → görünen ad: ürün adları ve gezilebilir kök/alt kategori adları; bulunamayan yol olduğu gibi yazılır.
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var (_, allProducts) = await products.GetAllAsync(cancellationToken);
        foreach (var product in allProducts.Data!)
        {
            names["/urun/" + product.Slug] = product.Name;
        }

        var (_, allCategories) = await categories.GetAllAsync(cancellationToken);
        foreach (var (rootSlug, rootPath) in StoreCatalog.BrowsableRoots)
        {
            var root = allCategories.Data!.FirstOrDefault(c => c.Slug == rootSlug && c.ParentId is null);
            if (root is null)
            {
                continue;
            }

            names[rootPath] = StoreCatalog.Root(root.Slug, root.Name).Name;
            foreach (var child in allCategories.Data!.Where(c => c.ParentId == root.Id))
            {
                names[rootPath + "/" + child.Slug] = child.Name;
            }
        }

        return View(new AnalyticsPageViewModel(report, names));
    }
}
