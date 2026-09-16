using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Utilities;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Business.Concrete;

public class BrandManager : IBrandService
{
    private const string RaceMessage = "Aynı adla bir marka az önce kaydedildi; sayfayı yenileyip yeniden deneyin.";

    private readonly IBrandDal _brandDal;
    private readonly IProductDal _productDal;
    private readonly IUnitOfWork _unitOfWork;

    public BrandManager(IBrandDal brandDal, IProductDal productDal, IUnitOfWork unitOfWork)
    {
        _brandDal = brandDal;
        _productDal = productDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Brand>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _brandDal.GetListAsync(cancellationToken: cancellationToken)).OrderBy(b => b.Name).ToList();

    public Task<Brand?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => _brandDal.GetAsync(b => b.Slug == slug, cancellationToken);

    public Task<Brand?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _brandDal.GetAsync(b => b.Id == id, cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<Brand>)> SaveAsync(Brand brand, CancellationToken cancellationToken = default)
    {
        var name = brand.Name.Trim();
        if (name.Length is 0 or > 60)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Brand>("Marka adı 1-60 karakter olmalı."));
        }

        if (await _brandDal.GetAsync(b => b.Name == name && b.Id != brand.Id, cancellationToken) is not null)
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<Brand>("Bu adda bir marka zaten var."));
        }

        var slug = await SlugGenerator.MakeUniqueAsync(
            SlugGenerator.Generate(name),
            async candidate => await _brandDal.GetAsync(b => b.Slug == candidate && b.Id != brand.Id, cancellationToken) is not null);

        if (brand.Id == 0)
        {
            var created = new Brand { Name = name, Slug = slug, LogoUrl = brand.LogoUrl };
            await _brandDal.AddAsync(created, cancellationToken);
            return await SavedAsync(name, slug, excludedId: 0, cancellationToken)
                ? (HttpStatusCode.Created, new SuccessDataResult<Brand>(created, "Marka eklendi."))
                : (HttpStatusCode.Conflict, new ErrorDataResult<Brand>(RaceMessage));
        }

        var stored = await _brandDal.GetTrackedAsync(b => b.Id == brand.Id, cancellationToken);
        if (stored is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<Brand>("Marka bulunamadı."));
        }

        stored.Name = name;
        stored.Slug = slug;
        stored.LogoUrl = brand.LogoUrl;
        return await SavedAsync(name, slug, stored.Id, cancellationToken)
            ? (HttpStatusCode.OK, new SuccessDataResult<Brand>(stored, "Marka güncellendi."))
            : (HttpStatusCode.Conflict, new ErrorDataResult<Brand>(RaceMessage));
    }

    /// <summary>Ad/slug denetimiyle kayıt arasında aynı adla eşzamanlı istek yazılırsa benzersiz indeks yakalar: 500 yerine 409.</summary>
    private async Task<bool> SavedAsync(string name, string slug, int excludedId, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            if ((await _brandDal.GetListAsync(b => (b.Name == name || b.Slug == slug) && b.Id != excludedId, cancellationToken)).Count == 0)
            {
                throw;
            }

            return false;
        }
    }

    public async Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var brand = await _brandDal.GetTrackedAsync(b => b.Id == id, cancellationToken);
        if (brand is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Marka bulunamadı."));
        }

        if (await _productDal.GetAsync(p => p.BrandId == id, cancellationToken) is not null)
        {
            return (HttpStatusCode.Conflict, new ErrorResult("Bu markanın ürünleri var; önce ürünlerin markasını değiştirin."));
        }

        _brandDal.Delete(brand);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Marka silindi."));
    }
}
