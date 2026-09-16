using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class AttributeManager : IAttributeService
{
    private readonly IProductAttributeDal _attributeDal;
    private readonly ICategoryAttributeTemplateDal _templateDal;
    private readonly IProductDal _productDal;
    private readonly ICategoryDal _categoryDal;
    private readonly IUnitOfWork _unitOfWork;

    public AttributeManager(
        IProductAttributeDal attributeDal,
        ICategoryAttributeTemplateDal templateDal,
        IProductDal productDal,
        ICategoryDal categoryDal,
        IUnitOfWork unitOfWork)
    {
        _attributeDal = attributeDal;
        _templateDal = templateDal;
        _productDal = productDal;
        _categoryDal = categoryDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ProductAttribute>> GetForProductAsync(int productId, CancellationToken cancellationToken = default)
        => (await _attributeDal.GetListAsync(a => a.ProductId == productId, cancellationToken)).OrderBy(a => a.SortOrder).ToList();

    public async Task<(HttpStatusCode, IResult)> SetForProductAsync(int productId, IReadOnlyList<AttributePair> pairs, CancellationToken cancellationToken = default)
    {
        if (await _productDal.GetAsync(p => p.Id == productId, cancellationToken) is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Ürün bulunamadı."));
        }

        await ReplaceAsync(_attributeDal, productId, pairs, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Özellikler kaydedildi."));
    }

    /// <summary>Kaydetmez: içe aktarma kendi işleminde çağırır.</summary>
    public static async Task ReplaceAsync(IProductAttributeDal attributeDal, int productId, IReadOnlyList<AttributePair> pairs, CancellationToken cancellationToken)
    {
        foreach (var stale in await attributeDal.GetListAsync(a => a.ProductId == productId, cancellationToken))
        {
            attributeDal.Delete(stale);
        }

        for (var i = 0; i < pairs.Count; i++)
        {
            await attributeDal.AddAsync(new ProductAttribute { ProductId = productId, Name = pairs[i].Name, Value = pairs[i].Value, SortOrder = i }, cancellationToken);
        }
    }

    public async Task<List<string>> GetTemplateAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var own = await NamesAsync(categoryId, cancellationToken);
        if (own.Count > 0)
        {
            return own;
        }

        return await _categoryDal.GetAsync(c => c.Id == categoryId, cancellationToken) is { ParentId: { } parentId }
            ? await NamesAsync(parentId, cancellationToken)
            : [];
    }

    public async Task SetTemplateAsync(int categoryId, IReadOnlyList<string> names, CancellationToken cancellationToken = default)
    {
        foreach (var stale in await _templateDal.GetListAsync(t => t.CategoryId == categoryId, cancellationToken))
        {
            _templateDal.Delete(stale);
        }

        var distinct = names
            .Select(n => n.Trim())
            .Where(n => n.Length is > 0 and <= AttributeText.NameLength)
            .Distinct(StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true))
            .ToList();
        for (var i = 0; i < distinct.Count; i++)
        {
            await _templateDal.AddAsync(new CategoryAttributeTemplate { CategoryId = categoryId, Name = distinct[i], SortOrder = i }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<string>> NamesAsync(int categoryId, CancellationToken cancellationToken)
        => (await _templateDal.GetListAsync(t => t.CategoryId == categoryId, cancellationToken)).OrderBy(t => t.SortOrder).Select(t => t.Name).ToList();
}
