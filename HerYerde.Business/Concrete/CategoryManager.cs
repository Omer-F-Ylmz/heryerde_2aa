using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Rules;
using HerYerde.Business.Utilities;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class CategoryManager : ICategoryService
{
    private readonly ICategoryDal _categoryDal;
    private readonly IProductDal _productDal;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryManager(ICategoryDal categoryDal, IProductDal productDal, IUnitOfWork unitOfWork)
    {
        _categoryDal = categoryDal;
        _productDal = productDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<(HttpStatusCode, IDataResult<List<Category>>)> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categoryDal.GetListAsync(cancellationToken: cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<List<Category>>(categories));
    }

    public async Task<(HttpStatusCode, IDataResult<Category>)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryDal.GetAsync(c => c.Id == id, cancellationToken);
        return category is null
            ? (HttpStatusCode.NotFound, new ErrorDataResult<Category>("Kategori bulunamadı."))
            : (HttpStatusCode.OK, new SuccessDataResult<Category>(category));
    }

    public async Task<(HttpStatusCode, IResult)> AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        if (await ParentIsInvalidAsync(category.ParentId, cancellationToken) is { } parentError)
        {
            return parentError;
        }

        category.Slug = await UniqueSlugAsync(category.Name, excludedId: 0, cancellationToken);
        await _categoryDal.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Kategori eklendi."));
    }

    public async Task<(HttpStatusCode, IResult)> UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        var stored = await _categoryDal.GetTrackedAsync(c => c.Id == category.Id, cancellationToken);
        if (stored is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Kategori bulunamadı."));
        }

        if (!CategoryRules.IsRoot(stored) && await ParentIsInvalidAsync(category.ParentId, cancellationToken) is { } parentError)
        {
            return parentError;
        }

        stored.Name = category.Name;
        stored.SortOrder = category.SortOrder;
        stored.IsActive = category.IsActive;

        // Kök kategori bir alanın kendisidir: adresi (slug) ve konumu sabit kalır.
        if (!CategoryRules.IsRoot(stored))
        {
            stored.ParentId = category.ParentId;
            stored.Slug = await UniqueSlugAsync(category.Name, stored.Id, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Kategori güncellendi."));
    }

    public async Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryDal.GetTrackedAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Kategori bulunamadı."));
        }

        if (CategoryRules.IsRoot(category))
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Kök kategori silinemez."));
        }

        if (await _productDal.GetAsync(p => p.CategoryId == id, cancellationToken) is not null)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Kategoride ürün var; önce ürünleri başka kategoriye taşıyın."));
        }

        if (await _categoryDal.GetAsync(c => c.ParentId == id, cancellationToken) is not null)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Kategorinin alt kategorileri var; önce onları silin."));
        }

        _categoryDal.Delete(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Kategori silindi."));
    }

    private async Task<(HttpStatusCode, IResult)?> ParentIsInvalidAsync(int? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        var parent = await _categoryDal.GetAsync(c => c.Id == parentId, cancellationToken);
        if (parent is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Üst kategori bulunamadı."));
        }

        return CategoryRules.CanBeParent(parent)
            ? null
            : (HttpStatusCode.BadRequest, new ErrorResult("Kategori ağacı iki seviyedir; alt kategorinin altına kategori açılmaz."));
    }

    private Task<string> UniqueSlugAsync(string name, int excludedId, CancellationToken cancellationToken)
        => SlugGenerator.MakeUniqueAsync(
            SlugGenerator.Generate(name),
            async slug => await _categoryDal.GetAsync(c => c.Slug == slug && c.Id != excludedId, cancellationToken) is not null);
}
