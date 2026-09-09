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
public class CategoriesController : Controller
{
    private readonly ICategoryService _categoryService;
    private readonly IProductService _productService;

    public CategoriesController(ICategoryService categoryService, IProductService productService)
    {
        _categoryService = categoryService;
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await BuildListAsync(errorMessage: null, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
        => View("Form", new CategoryFormViewModel { Parents = await RootsAsync(0, cancellationToken) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Parents = await RootsAsync(0, cancellationToken);
            return View("Form", model);
        }

        var (status, result) = await _categoryService.AddAsync(new Category
        {
            Name = model.Name,
            ParentId = model.ParentId,
            SortOrder = model.SortOrder,
            IsActive = model.IsActive
        }, cancellationToken);

        if (status == HttpStatusCode.Created)
        {
            return RedirectToAction(nameof(Index));
        }

        model.Parents = await RootsAsync(0, cancellationToken);
        model.ErrorMessage = result.Message;
        Response.StatusCode = (int)status;
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var (status, result) = await _categoryService.GetByIdAsync(id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        var category = result.Data!;
        return View("Form", new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            ParentId = category.ParentId,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            IsRoot = CategoryRules.IsRoot(category),
            Parents = await RootsAsync(category.Id, cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Parents = await RootsAsync(model.Id, cancellationToken);
            return View("Form", model);
        }

        var (status, result) = await _categoryService.UpdateAsync(new Category
        {
            Id = model.Id,
            Name = model.Name,
            ParentId = model.ParentId,
            SortOrder = model.SortOrder,
            IsActive = model.IsActive
        }, cancellationToken);

        if (status == HttpStatusCode.OK)
        {
            return RedirectToAction(nameof(Index));
        }

        model.Parents = await RootsAsync(model.Id, cancellationToken);
        model.ErrorMessage = result.Message;
        Response.StatusCode = (int)status;
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var (status, result) = await _categoryService.DeleteAsync(id, cancellationToken);
        if (status == HttpStatusCode.OK)
        {
            return RedirectToAction(nameof(Index));
        }

        Response.StatusCode = (int)status;
        return View("Index", await BuildListAsync(result.Message, cancellationToken));
    }

    private async Task<CategoryListViewModel> BuildListAsync(string? errorMessage, CancellationToken cancellationToken)
    {
        var (_, categories) = await _categoryService.GetAllAsync(cancellationToken);
        var (_, products) = await _productService.GetAllAsync(cancellationToken);
        var all = categories.Data!;

        return new CategoryListViewModel
        {
            Categories = all
                .OrderBy(c => c.ParentId ?? c.Id)
                .ThenBy(c => c.ParentId is null ? 0 : 1)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList(),
            RootNames = all.Where(c => c.ParentId is null).ToDictionary(c => c.Id, c => c.Name),
            ProductCounts = products.Data!
                .GroupBy(p => p.CategoryId)
                .ToDictionary(group => group.Key, group => group.Count()),
            ErrorMessage = errorMessage
        };
    }

    /// <summary>Ağaç iki seviye: üst kategori olarak yalnız kökler seçilebilir.</summary>
    private async Task<List<Category>> RootsAsync(int excludedId, CancellationToken cancellationToken)
    {
        var (_, categories) = await _categoryService.GetAllAsync(cancellationToken);
        return categories.Data!
            .Where(c => c.ParentId is null && c.Id != excludedId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToList();
    }
}
