using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Rules;
using HerYerde.Business.Utilities;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class ProductManager : IProductService
{
    private readonly IProductDal _productDal;
    private readonly IProductVariantDal _variantDal;
    private readonly IProductImageDal _imageDal;
    private readonly ICategoryDal _categoryDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductManager(
        IProductDal productDal,
        IProductVariantDal variantDal,
        IProductImageDal imageDal,
        ICategoryDal categoryDal,
        IUnitOfWork unitOfWork)
    {
        _productDal = productDal;
        _variantDal = variantDal;
        _imageDal = imageDal;
        _categoryDal = categoryDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<(HttpStatusCode, IDataResult<List<Product>>)> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productDal.GetListAsync(cancellationToken: cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<List<Product>>(products));
    }

    public async Task<(HttpStatusCode, IDataResult<Product>)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productDal.GetAsync(p => p.Id == id, cancellationToken);
        return product is null
            ? (HttpStatusCode.NotFound, new ErrorDataResult<Product>("Ürün bulunamadı."))
            : (HttpStatusCode.OK, new SuccessDataResult<Product>(product));
    }

    public async Task<(HttpStatusCode, IDataResult<Product>)> AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        var (rootStatus, root) = await RootOfAsync(product.CategoryId, cancellationToken);
        if (root is null)
        {
            return (rootStatus, new ErrorDataResult<Product>("Kategori bulunamadı."));
        }

        if (product.IsActive && ProductRules.RequiresVariants(root))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Product>(VariantRequiredMessage));
        }

        product.Slug = await UniqueSlugAsync(product.Name, excludedId: 0, cancellationToken);
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = product.CreatedAt;

        await _productDal.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessDataResult<Product>(product, "Ürün eklendi."));
    }

    public async Task<(HttpStatusCode, IResult)> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        var stored = await _productDal.GetTrackedAsync(p => p.Id == product.Id, cancellationToken);
        if (stored is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Ürün bulunamadı."));
        }

        var (rootStatus, root) = await RootOfAsync(product.CategoryId, cancellationToken);
        if (root is null)
        {
            return (rootStatus, new ErrorResult("Kategori bulunamadı."));
        }

        if (product.IsActive && ProductRules.RequiresVariants(root) && !await HasVariantAsync(stored.Id, cancellationToken))
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(VariantRequiredMessage));
        }

        stored.Name = product.Name;
        stored.Description = product.Description;
        stored.CategoryId = product.CategoryId;
        stored.Price = product.Price;
        stored.CampaignPrice = product.CampaignPrice;
        stored.CampaignLabel = product.CampaignLabel;
        stored.CampaignEndsAt = product.CampaignEndsAt;
        stored.IsActive = product.IsActive;
        stored.Slug = await UniqueSlugAsync(product.Name, stored.Id, cancellationToken);
        stored.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Ürün güncellendi."));
    }

    public async Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productDal.GetTrackedAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Ürün bulunamadı."));
        }

        product.IsActive = false;
        product.DeletedAt = DateTime.UtcNow;
        product.UpdatedAt = product.DeletedAt.Value;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Ürün silindi."));
    }

    public async Task<(HttpStatusCode, IDataResult<List<ProductVariant>>)> GetVariantsAsync(int productId, CancellationToken cancellationToken = default)
    {
        var variants = await _variantDal.GetListAsync(v => v.ProductId == productId, cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<List<ProductVariant>>(variants));
    }

    public async Task<(HttpStatusCode, IResult)> AddVariantAsync(ProductVariant variant, CancellationToken cancellationToken = default)
    {
        if (variant.Stock < 0)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Stok negatif olamaz."));
        }

        var product = await _productDal.GetAsync(p => p.Id == variant.ProductId, cancellationToken);
        if (product is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Ürün bulunamadı."));
        }

        var (rootStatus, root) = await RootOfAsync(product.CategoryId, cancellationToken);
        if (root is null)
        {
            return (rootStatus, new ErrorResult("Kategori bulunamadı."));
        }

        if (!ProductRules.RequiresVariants(root) && (!string.IsNullOrWhiteSpace(variant.Size) || !string.IsNullOrWhiteSpace(variant.Color)))
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Ev ürününde beden ve renk boş kalmalı."));
        }

        if (await _variantDal.GetAsync(v => v.Sku == variant.Sku, cancellationToken) is not null)
        {
            return (HttpStatusCode.Conflict, new ErrorResult($"'{variant.Sku}' stok kodu başka bir varyantta kullanılıyor."));
        }

        variant.Size = NullIfBlank(variant.Size);
        variant.Color = NullIfBlank(variant.Color);

        await _variantDal.AddAsync(variant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Varyant eklendi."));
    }

    public async Task<(HttpStatusCode, IResult)> UpdateStockAsync(int variantId, int stock, CancellationToken cancellationToken = default)
    {
        if (stock < 0)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Stok negatif olamaz."));
        }

        var variant = await _variantDal.GetTrackedAsync(v => v.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Varyant bulunamadı."));
        }

        variant.Stock = stock;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Stok güncellendi."));
    }

    public async Task<(HttpStatusCode, IResult)> DeleteVariantAsync(int variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _variantDal.GetTrackedAsync(v => v.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Varyant bulunamadı."));
        }

        _variantDal.Delete(variant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Varyant silindi."));
    }

    public async Task<(HttpStatusCode, IDataResult<List<ProductImage>>)> GetImagesAsync(int productId, CancellationToken cancellationToken = default)
    {
        var images = await _imageDal.GetListAsync(i => i.ProductId == productId, cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<List<ProductImage>>(images));
    }

    public async Task<(HttpStatusCode, IResult)> AddImageAsync(ProductImage image, CancellationToken cancellationToken = default)
    {
        if (await _productDal.GetAsync(p => p.Id == image.ProductId, cancellationToken) is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Ürün bulunamadı."));
        }

        await _imageDal.AddAsync(image, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (image.IsPrimary)
        {
            await DemoteOtherPrimariesAsync(image.ProductId, image.Id, cancellationToken);
        }

        return (HttpStatusCode.Created, new SuccessResult("Görsel eklendi."));
    }

    public async Task<(HttpStatusCode, IResult)> UpdateImageSortAsync(int imageId, int sortOrder, CancellationToken cancellationToken = default)
    {
        var image = await _imageDal.GetTrackedAsync(i => i.Id == imageId, cancellationToken);
        if (image is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Görsel bulunamadı."));
        }

        image.SortOrder = sortOrder;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Görsel sırası güncellendi."));
    }

    public async Task<(HttpStatusCode, IResult)> SetPrimaryImageAsync(int imageId, CancellationToken cancellationToken = default)
    {
        var image = await _imageDal.GetTrackedAsync(i => i.Id == imageId, cancellationToken);
        if (image is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Görsel bulunamadı."));
        }

        image.IsPrimary = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DemoteOtherPrimariesAsync(image.ProductId, image.Id, cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Birincil görsel güncellendi."));
    }

    public async Task<(HttpStatusCode, IResult)> DeleteImageAsync(int imageId, CancellationToken cancellationToken = default)
    {
        var image = await _imageDal.GetTrackedAsync(i => i.Id == imageId, cancellationToken);
        if (image is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Görsel bulunamadı."));
        }

        _imageDal.Delete(image);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Görsel silindi."));
    }

    private const string VariantRequiredMessage = "Giyim ürünü en az bir varyant olmadan yayına alınamaz.";

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private async Task DemoteOtherPrimariesAsync(int productId, int primaryImageId, CancellationToken cancellationToken)
    {
        var others = await _imageDal.GetListAsync(i => i.ProductId == productId && i.Id != primaryImageId && i.IsPrimary, cancellationToken);
        foreach (var other in others)
        {
            var tracked = await _imageDal.GetTrackedAsync(i => i.Id == other.Id, cancellationToken);
            tracked!.IsPrimary = false;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasVariantAsync(int productId, CancellationToken cancellationToken)
        => await _variantDal.GetAsync(v => v.ProductId == productId, cancellationToken) is not null;

    /// <summary>Ürünün kök alanını (Giyim | Ev) bulur; kategori iki seviye olduğu için en fazla bir adım yukarı çıkar.</summary>
    private async Task<(HttpStatusCode, Category?)> RootOfAsync(int categoryId, CancellationToken cancellationToken)
    {
        var category = await _categoryDal.GetAsync(c => c.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return (HttpStatusCode.NotFound, null);
        }

        if (category.ParentId is null)
        {
            return (HttpStatusCode.OK, category);
        }

        var root = await _categoryDal.GetAsync(c => c.Id == category.ParentId, cancellationToken);
        return root is null ? (HttpStatusCode.NotFound, null) : (HttpStatusCode.OK, root);
    }

    private Task<string> UniqueSlugAsync(string name, int excludedId, CancellationToken cancellationToken)
        => SlugGenerator.MakeUniqueAsync(
            SlugGenerator.Generate(name),
            async slug => await _productDal.GetAsync(p => p.Slug == slug && p.Id != excludedId, cancellationToken) is not null);
}
