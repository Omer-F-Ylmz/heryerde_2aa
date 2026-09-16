using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class ProductsController : Controller
{
    private const string Entity = "ürün";

    /// <summary>Listede "Fiyat eksik" süzgecini açan sorgu değeri (/admin/products?fiyat=eksik).</summary>
    private const string PriceMissingFilter = "eksik";

    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IAdminAuditService _auditService;
    private readonly IProductImageStorage _imageStorage;
    private readonly IProductVideoStorage _videoStorage;
    private readonly TimeProvider _clock;

    public ProductsController(
        IProductService productService,
        ICategoryService categoryService,
        IAdminAuditService auditService,
        IProductImageStorage imageStorage,
        IProductVideoStorage videoStorage,
        TimeProvider clock)
    {
        _productService = productService;
        _categoryService = categoryService;
        _auditService = auditService;
        _imageStorage = imageStorage;
        _videoStorage = videoStorage;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? fiyat, CancellationToken cancellationToken)
    {
        var (_, products) = await _productService.GetAllAsync(cancellationToken);
        var (_, categories) = await _categoryService.GetAllAsync(cancellationToken);

        var (_, stockTotals) = await _productService.GetStockTotalsAsync(cancellationToken);
        var priceMissingOnly = fiyat == PriceMissingFilter;

        return View(new ProductListViewModel
        {
            PriceMissingOnly = priceMissingOnly,
            Products = products.Data!
                .Where(p => !priceMissingOnly || ProductRules.PriceMissing(p))
                .OrderByDescending(p => p.UpdatedAt)
                .ToList(),
            CategoryNames = categories.Data!.ToDictionary(c => c.Id, c => c.Name),
            StockTotals = stockTotals.Data!,
            Now = DateTime.UtcNow
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
        => View(new ProductFormViewModel
        {
            Categories = await CategoriesAsync(cancellationToken),
            GiftProducts = await GiftProductsAsync(0, cancellationToken)
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await FillListsAsync(model, cancellationToken);
            return View(model);
        }

        var (status, created) = await _productService.AddAsync(ToProduct(model), cancellationToken);
        if (status != HttpStatusCode.Created)
        {
            await FillListsAsync(model, cancellationToken);
            model.ErrorMessage = created.Message;
            Response.StatusCode = (int)status;
            return View(model);
        }

        await _auditService.WriteAsync(HttpContext, "ekle", Entity, created.Data!.Id);
        return RedirectToAction(nameof(Edit), new { id = created.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var model = await BuildFormAsync(id, errorMessage: null, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await FillListsAsync(model, cancellationToken);
            return View(model);
        }

        var (status, result) = await _productService.UpdateAsync(ToProduct(model), cancellationToken);
        if (status == HttpStatusCode.OK)
        {
            await _auditService.WriteAsync(HttpContext, "güncelle", Entity, model.Id);
            return RedirectToAction(nameof(Index));
        }

        if (status == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        await FillListsAsync(model, cancellationToken);
        model.ErrorMessage = result.Message;
        Response.StatusCode = (int)status;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await AuditIfDoneAsync(await _productService.DeleteAsync(id, cancellationToken), "sil", id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVariant(VariantFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await FormWithErrorAsync(model.ProductId, "Varyant alanlarını kontrol edin.", cancellationToken);
        }

        var (status, result) = await _productService.AddVariantAsync(new ProductVariant
        {
            ProductId = model.ProductId,
            Size = model.Size,
            Color = model.Color,
            Sku = model.Sku,
            Stock = model.Stock
        }, cancellationToken);

        if (status != HttpStatusCode.Created)
        {
            return await FormWithErrorAsync(model.ProductId, result.Message, cancellationToken, status);
        }

        await _auditService.WriteAsync(HttpContext, "varyant ekle", Entity, model.ProductId);
        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStock(int productId, int variantId, int stock, CancellationToken cancellationToken)
    {
        var (status, result) = await _productService.UpdateStockAsync(variantId, stock, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return await FormWithErrorAsync(productId, result.Message, cancellationToken, status);
        }

        await _auditService.WriteAsync(HttpContext, "stok güncelle", Entity, productId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVariant(int productId, int variantId, CancellationToken cancellationToken)
    {
        await AuditIfDoneAsync(await _productService.DeleteVariantAsync(variantId, cancellationToken), "varyant sil", productId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImage(ImageFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Files is not { Count: > 0 })
        {
            return await FormWithErrorAsync(model.ProductId, "Görsel alanlarını kontrol edin.", cancellationToken);
        }

        // Hiçbir dosya işlenmeden önce hepsi denetlenir; yarım yüklenmiş küme kalmaz.
        foreach (var file in model.Files)
        {
            if (await ImageFile.ProblemAsync(file, cancellationToken) is { } problem)
            {
                return await FormWithErrorAsync(model.ProductId, problem, cancellationToken);
            }
        }

        var sortOrder = model.SortOrder;
        foreach (var file in model.Files)
        {
            await using var content = file.OpenReadStream();
            var added = await _productService.AddImageAsync(new ProductImage
            {
                ProductId = model.ProductId,
                Url = await _imageStorage.SaveAsync(model.ProductId, content, cancellationToken),
                Alt = model.Alt,
                SortOrder = sortOrder++
            }, cancellationToken);

            await AuditIfDoneAsync(added, "görsel ekle", model.ProductId);
        }

        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(VideoFile.MaxBytes + 64_000)]
    public async Task<IActionResult> AddVideo(VideoFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.Video is not { } file)
        {
            return await FormWithErrorAsync(model.ProductId, "Video dosyası seçin.", cancellationToken);
        }

        if (await VideoFile.ProblemAsync(file, cancellationToken) is { } problem)
        {
            return await FormWithErrorAsync(model.ProductId, problem, cancellationToken);
        }

        await using var content = file.OpenReadStream();
        // Süre ancak çözülerek anlaşılır: sığmayan kaynak diske hiçbir şey yazmadan geri döner.
        if (await _videoStorage.SaveAsync(model.ProductId, content, cancellationToken) is not { } assets)
        {
            return await FormWithErrorAsync(model.ProductId, "Video 90 saniyeden uzun olamaz.", cancellationToken);
        }

        var added = await _productService.AddVideoAsync(new ProductVideo
        {
            ProductId = model.ProductId,
            Url = assets.Url,
            PosterUrl = assets.PosterUrl,
            PreviewUrl = assets.PreviewUrl,
            Duration = assets.Duration,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        }, cancellationToken);

        if (added.Item1 != HttpStatusCode.Created)
        {
            _videoStorage.Delete(assets.Url);
            return await FormWithErrorAsync(model.ProductId, added.Item2.Message, cancellationToken);
        }

        await AuditIfDoneAsync(added, "video ekle", model.ProductId);
        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVideo(int productId, int videoId, CancellationToken cancellationToken)
    {
        var (_, videos) = await _productService.GetVideosAsync(productId, cancellationToken);
        var url = videos.Data!.FirstOrDefault(v => v.Id == videoId)?.Url;

        var outcome = await _productService.DeleteVideoAsync(videoId, cancellationToken);
        if (outcome.Item1 == HttpStatusCode.OK && url is not null)
        {
            _videoStorage.Delete(url);
        }

        await AuditIfDoneAsync(outcome, "video sil", productId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateImageSort(int productId, int imageId, int sortOrder, CancellationToken cancellationToken)
    {
        await AuditIfDoneAsync(await _productService.UpdateImageSortAsync(imageId, sortOrder, cancellationToken), "görsel sırası", productId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(int productId, int imageId, CancellationToken cancellationToken)
    {
        await AuditIfDoneAsync(await _productService.SetPrimaryImageAsync(imageId, cancellationToken), "birincil görsel", productId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int productId, int imageId, CancellationToken cancellationToken)
    {
        var (_, images) = await _productService.GetImagesAsync(productId, cancellationToken);
        var url = images.Data!.FirstOrDefault(i => i.Id == imageId)?.Url;

        var outcome = await _productService.DeleteImageAsync(imageId, cancellationToken);
        if (outcome.Item1 == HttpStatusCode.OK && url is not null)
        {
            _imageStorage.Delete(url);
        }

        await AuditIfDoneAsync(outcome, "görsel sil", productId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    /// <summary>Sonucu sayfaya yansımayan işlemler: yalnız başarılıysa denetim izine düşer.</summary>
    private async Task AuditIfDoneAsync((HttpStatusCode Status, HerYerde.Core.Utilities.Results.IResult) outcome, string action, int productId)
    {
        if (outcome.Status is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            await _auditService.WriteAsync(HttpContext, action, Entity, productId);
        }
    }

    private static Product ToProduct(ProductFormViewModel model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        CategoryId = model.CategoryId,
        Price = model.Price,
        CampaignPrice = model.CampaignPrice,
        CampaignLabel = string.IsNullOrWhiteSpace(model.CampaignLabel) ? null : model.CampaignLabel,
        CampaignEndsAt = model.CampaignEndsAt,
        Dimensions = string.IsNullOrWhiteSpace(model.Dimensions) ? null : model.Dimensions.Trim(),
        GiftMode = model.GiftMode,
        GiftProductId = model.GiftProductId,
        GiftQty = model.GiftQty,
        Stock = model.Stock,
        VariantAxis1Label = model.VariantAxis1Label,
        VariantAxis2Label = model.VariantAxis2Label,
        IsActive = model.IsActive,
        IsFeatured = model.IsFeatured,
        FeaturedOrder = model.FeaturedOrder
    };

    private async Task<IActionResult> FormWithErrorAsync(
        int productId,
        string message,
        CancellationToken cancellationToken,
        HttpStatusCode status = HttpStatusCode.BadRequest)
    {
        var model = await BuildFormAsync(productId, message, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        Response.StatusCode = (int)status;
        return View("Edit", model);
    }

    private async Task<ProductFormViewModel?> BuildFormAsync(int id, string? errorMessage, CancellationToken cancellationToken)
    {
        var (status, stored) = await _productService.GetByIdAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return null;
        }

        var product = stored.Data!;
        var model = new ProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            Price = product.Price,
            CampaignPrice = product.CampaignPrice,
            CampaignLabel = product.CampaignLabel,
            CampaignEndsAt = product.CampaignEndsAt,
            Dimensions = product.Dimensions,
            GiftMode = product.GiftMode,
            GiftProductId = product.GiftProductId,
            GiftQty = product.GiftQty,
            Stock = product.Stock,
            VariantAxis1Label = product.VariantAxis1Label,
            VariantAxis2Label = product.VariantAxis2Label,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            FeaturedOrder = product.FeaturedOrder,
            Slug = product.Slug,
            ErrorMessage = errorMessage
        };

        await FillListsAsync(model, cancellationToken);
        return model;
    }

    private async Task FillListsAsync(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        var (_, variants) = await _productService.GetVariantsAsync(model.Id, cancellationToken);
        var (_, images) = await _productService.GetImagesAsync(model.Id, cancellationToken);
        var (_, videos) = await _productService.GetVideosAsync(model.Id, cancellationToken);

        model.Categories = await CategoriesAsync(cancellationToken);
        model.GiftProducts = await GiftProductsAsync(model.Id, cancellationToken);
        model.Variants = variants.Data!.OrderBy(v => v.Size).ThenBy(v => v.Color).ThenBy(v => v.Sku).ToList();
        model.Images = images.Data!.OrderBy(i => i.SortOrder).ToList();
        model.Videos = videos.Data!.OrderBy(v => v.SortOrder).ToList();
        model.RequiresVariants = await RequiresVariantsAsync(model.CategoryId, cancellationToken);
    }

    /// <summary>Hediye olarak seçilebilecek ürünler; ürün kendini hediye edemez.</summary>
    private async Task<List<Product>> GiftProductsAsync(int excludedId, CancellationToken cancellationToken)
    {
        var (_, products) = await _productService.GetAllAsync(cancellationToken);
        return products.Data!.Where(p => p.Id != excludedId).OrderBy(p => p.Name).ToList();
    }

    private async Task<bool> RequiresVariantsAsync(int categoryId, CancellationToken cancellationToken)
    {
        var (_, categories) = await _categoryService.GetAllAsync(cancellationToken);
        var category = categories.Data!.FirstOrDefault(c => c.Id == categoryId);
        if (category is null)
        {
            return false;
        }

        var root = category.ParentId is null ? category : categories.Data!.FirstOrDefault(c => c.Id == category.ParentId);
        return root is not null && ProductRules.RequiresVariants(root);
    }

    private async Task<List<Category>> CategoriesAsync(CancellationToken cancellationToken)
    {
        var (_, categories) = await _categoryService.GetAllAsync(cancellationToken);
        return categories.Data!.OrderBy(c => c.ParentId ?? c.Id).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
    }
}
