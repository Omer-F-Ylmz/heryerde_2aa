using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class ProductsController : Controller
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public ProductsController(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
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
        await _productService.DeleteAsync(id, cancellationToken);
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

        return status == HttpStatusCode.Created
            ? RedirectToAction(nameof(Edit), new { id = model.ProductId })
            : await FormWithErrorAsync(model.ProductId, result.Message, cancellationToken, status);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStock(int productId, int variantId, int stock, CancellationToken cancellationToken)
    {
        var (status, result) = await _productService.UpdateStockAsync(variantId, stock, cancellationToken);

        return status == HttpStatusCode.OK
            ? RedirectToAction(nameof(Edit), new { id = productId })
            : await FormWithErrorAsync(productId, result.Message, cancellationToken, status);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVariant(int productId, int variantId, CancellationToken cancellationToken)
    {
        await _productService.DeleteVariantAsync(variantId, cancellationToken);
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

        await _productService.AddImageAsync(new ProductImage
        {
            ProductId = model.ProductId,
            Url = model.Url,
            Alt = model.Alt,
            SortOrder = model.SortOrder
        }, cancellationToken);

        return RedirectToAction(nameof(Edit), new { id = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateImageSort(int productId, int imageId, int sortOrder, CancellationToken cancellationToken)
    {
        await _productService.UpdateImageSortAsync(imageId, sortOrder, cancellationToken);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(int productId, int imageId, CancellationToken cancellationToken)
    {
        await _productService.SetPrimaryImageAsync(imageId, cancellationToken);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int productId, int imageId, CancellationToken cancellationToken)
    {
        await _productService.DeleteImageAsync(imageId, cancellationToken);
        return RedirectToAction(nameof(Edit), new { id = productId });
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
