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

    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IAdminAuditService _auditService;

    public ProductsController(IProductService productService, ICategoryService categoryService, IAdminAuditService auditService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (_, products) = await _productService.GetAllAsync(cancellationToken);
        var (_, categories) = await _categoryService.GetAllAsync(cancellationToken);

        var (_, stockTotals) = await _productService.GetStockTotalsAsync(cancellationToken);

        return View(new ProductListViewModel
        {
            Products = products.Data!.OrderByDescending(p => p.UpdatedAt).ToList(),
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
        if (!ModelState.IsValid)
        {
            return await FormWithErrorAsync(model.ProductId, "Görsel alanlarını kontrol edin.", cancellationToken);
        }

        var added = await _productService.AddImageAsync(new ProductImage
        {
            ProductId = model.ProductId,
            Url = model.Url,
            Alt = model.Alt,
            SortOrder = model.SortOrder
        }, cancellationToken);

        await AuditIfDoneAsync(added, "görsel ekle", model.ProductId);
        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
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
        await AuditIfDoneAsync(await _productService.DeleteImageAsync(imageId, cancellationToken), "görsel sil", productId);
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
        GiftMode = model.GiftMode,
        GiftProductId = model.GiftProductId,
        GiftQty = model.GiftQty,
        Stock = model.Stock,
        IsActive = model.IsActive
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
            GiftMode = product.GiftMode,
            GiftProductId = product.GiftProductId,
            GiftQty = product.GiftQty,
            Stock = product.Stock,
            IsActive = product.IsActive,
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

        model.Categories = await CategoriesAsync(cancellationToken);
        model.GiftProducts = await GiftProductsAsync(model.Id, cancellationToken);
        model.Variants = variants.Data!.OrderBy(v => v.Size).ThenBy(v => v.Color).ThenBy(v => v.Sku).ToList();
        model.Images = images.Data!.OrderBy(i => i.SortOrder).ToList();
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
