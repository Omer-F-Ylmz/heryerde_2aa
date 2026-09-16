using System.Globalization;
using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Business.Utilities;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using Microsoft.Extensions.Options;

namespace HerYerde.Business.Concrete;

public class ProductTransferManager : IProductTransferService
{
    private readonly IProductDal _productDal;
    private readonly IProductVariantDal _variantDal;
    private readonly IProductImageDal _imageDal;
    private readonly IProductAttributeDal _attributeDal;
    private readonly ICategoryDal _categoryDal;
    private readonly ISlugHistoryDal _slugHistoryDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ShopSettings _shop;
    private readonly TimeProvider _clock;

    public ProductTransferManager(
        IProductDal productDal,
        IProductVariantDal variantDal,
        IProductImageDal imageDal,
        IProductAttributeDal attributeDal,
        ICategoryDal categoryDal,
        ISlugHistoryDal slugHistoryDal,
        IUnitOfWork unitOfWork,
        IOptions<ShopSettings> shop,
        TimeProvider clock)
    {
        _productDal = productDal;
        _variantDal = variantDal;
        _imageDal = imageDal;
        _attributeDal = attributeDal;
        _categoryDal = categoryDal;
        _slugHistoryDal = slugHistoryDal;
        _unitOfWork = unitOfWork;
        _shop = shop.Value;
        _clock = clock;
    }

    public async Task<List<ProductSheetRow>> ExportAsync(CancellationToken cancellationToken = default)
    {
        var products = (await _productDal.GetListAsync(null, cancellationToken)).OrderBy(p => p.Id).ToList();
        var categories = (await _categoryDal.GetListAsync(null, cancellationToken)).ToDictionary(c => c.Id, c => c.Slug);
        var variants = (await _variantDal.GetListAsync(null, cancellationToken)).ToLookup(v => v.ProductId);
        var images = (await _imageDal.GetListAsync(null, cancellationToken)).ToLookup(i => i.ProductId);
        var attributes = (await _attributeDal.GetListAsync(null, cancellationToken)).ToLookup(a => a.ProductId);

        var rows = new List<ProductSheetRow>();
        foreach (var product in products)
        {
            rows.Add(new ProductSheetRow(
                rows.Count + 2,
                Text(product.Id),
                product.Name,
                product.Slug,
                categories.GetValueOrDefault(product.CategoryId, string.Empty),
                product.Description,
                Text(product.Price),
                product.CampaignPrice is { } campaign ? Text(campaign) : string.Empty,
                product.CampaignLabel ?? string.Empty,
                product.CampaignEndsAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
                product.Stock is { } stock ? Text(stock) : string.Empty,
                product.IsActive ? "1" : "0",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Join("; ", images[product.Id].OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Url)),
                product.Dimensions ?? string.Empty,
                AttributeText.Format(attributes[product.Id].OrderBy(a => a.SortOrder).Select(a => new AttributePair(a.Name, a.Value)), ";")));

            foreach (var variant in variants[product.Id].OrderBy(v => v.Id))
            {
                rows.Add(new ProductSheetRow(
                    rows.Count + 2, string.Empty, string.Empty, product.Slug, string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty,
                    variant.Sku, variant.Size ?? string.Empty, variant.Color ?? string.Empty, Text(variant.Stock), string.Empty, string.Empty));
            }
        }

        return rows;
    }

    public async Task<ImportPreview> PreviewAsync(IReadOnlyList<ProductSheetRow> rows, CancellationToken cancellationToken = default)
        => new((await PlanAsync(rows, cancellationToken)).Select(p => p.Result).ToList());

    public async Task<(HttpStatusCode, IDataResult<ImportPreview>)> ApplyAsync(IReadOnlyList<ProductSheetRow> rows, CancellationToken cancellationToken = default)
    {
        var plan = await PlanAsync(rows, cancellationToken);
        var preview = new ImportPreview(plan.Select(p => p.Result).ToList());
        if (preview.Errors > 0 || plan.Count == 0)
        {
            // Önizleme hatayla da döner: sayfa hatalı satırları yeniden gösterir.
            return (HttpStatusCode.BadRequest, new DataResult<ImportPreview>(preview, false,
                plan.Count == 0 ? "Tabloda satır yok." : $"{preview.Errors} satır hatalı; hiçbir değişiklik yazılmadı."));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var removedImages = new List<string>();
        await _unitOfWork.InTransactionAsync(async () =>
        {
            var bySlug = new Dictionary<string, Product>(StringComparer.Ordinal);
            foreach (var step in plan.Where(p => !p.Row.IsVariant))
            {
                var row = step.Row;
                var product = step.ExistingProductId is { } id
                    ? (await _productDal.GetTrackedAsync(p => p.Id == id, cancellationToken))!
                    : new Product { CreatedAt = now, GiftQty = 1 };

                if (product.Id > 0 && product.Slug != step.Slug)
                {
                    await _slugHistoryDal.RecordAsync(SlugEntity.Product, product.Id, product.Slug, step.Slug, now, cancellationToken);
                }

                product.Name = row.Name.Trim();
                product.Slug = step.Slug;
                product.CategoryId = step.CategoryId;
                product.Description = row.Description.Trim();
                product.Price = Decimal(row.Price)!.Value;
                product.CampaignPrice = Decimal(row.CampaignPrice);
                product.CampaignLabel = Blank(row.CampaignLabel);
                product.CampaignEndsAt = Date(row.CampaignEndsAt);
                if (product.Id == 0 || row.ExportedStock is null || Integer(row.ExportedStock) != Integer(row.Stock))
                {
                    product.Stock = Integer(row.Stock);
                }

                product.IsActive = Flag(row.IsActive);
                product.Dimensions = Blank(row.Dimensions);
                product.UpdatedAt = now;
                if (product.Id == 0)
                {
                    await _productDal.AddAsync(product, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                bySlug[product.Slug] = product;

                if (step.Images is { } urls)
                {
                    removedImages.AddRange(await ReplaceImagesAsync(product, urls, cancellationToken));
                }

                // Boş kolon özelliklere dokunmaz: eski şablonla hazırlanmış tablo özellikleri silmesin.
                if (row.Attributes.Trim().Length > 0)
                {
                    await AttributeManager.ReplaceAsync(_attributeDal, product.Id, AttributeText.Parse(row.Attributes).Pairs, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }

            foreach (var step in plan.Where(p => p.Row.IsVariant))
            {
                var row = step.Row;
                var productId = bySlug.TryGetValue(step.Slug, out var inFile) ? inFile.Id : step.ExistingProductId!.Value;
                var variant = step.ExistingVariantId is { } variantId
                    ? (await _variantDal.GetTrackedAsync(v => v.Id == variantId, cancellationToken))!
                    : new ProductVariant { ProductId = productId, Sku = row.Sku.Trim() };

                variant.Size = Blank(row.Axis1);
                variant.Color = Blank(row.Axis2);
                if (variant.Id == 0 || row.ExportedStock is null || Integer(row.ExportedStock) != Integer(row.VariantStock))
                {
                    variant.Stock = Integer(row.VariantStock)!.Value;
                }

                if (variant.Id == 0)
                {
                    await _variantDal.AddAsync(variant, cancellationToken);
                }
            }

            return await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return (HttpStatusCode.OK, new SuccessDataResult<ImportPreview>(
            preview with { RemovedImages = removedImages },
            $"{preview.Created} yeni, {preview.Updated} güncellendi."));
    }

    /// <summary>Her satırın doğrulaması ve yazım planı. Veritabanı yalnız okunur.</summary>
    private async Task<List<Step>> PlanAsync(IReadOnlyList<ProductSheetRow> rows, CancellationToken cancellationToken)
    {
        var products = await _productDal.GetListAsync(null, cancellationToken);
        var productsById = products.ToDictionary(p => p.Id);
        var productsBySlug = products.ToDictionary(p => p.Slug, StringComparer.Ordinal);
        var categories = await _categoryDal.GetListAsync(null, cancellationToken);
        var categoriesBySlug = categories.ToDictionary(c => c.Slug, StringComparer.Ordinal);
        var variants = await _variantDal.GetListAsync(null, cancellationToken);
        var variantsBySku = variants.ToDictionary(v => v.Sku, StringComparer.Ordinal);
        var productsWithVariants = variants.Select(v => v.ProductId).ToHashSet();

        var variantSlugsInFile = rows.Where(r => r.IsVariant).Select(r => r.Slug.Trim()).ToHashSet(StringComparer.Ordinal);
        var slugsInFile = new HashSet<string>(StringComparer.Ordinal);
        var skusInFile = new HashSet<string>(StringComparer.Ordinal);
        var plan = new List<Step>();

        foreach (var row in rows.Where(r => !r.IsVariant))
        {
            string? Problem(out Step step)
            {
                step = new Step(row, new ImportRowResult(row.RowNumber, ImportRowKind.Hata, Label(row), null));
                Product? existing = null;
                if (row.Id.Trim().Length > 0)
                {
                    if (!int.TryParse(row.Id.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !productsById.TryGetValue(id, out existing))
                    {
                        return $"id {row.Id.Trim()} ile ürün bulunamadı.";
                    }
                }

                var slug = row.Slug.Trim().Length > 0 ? row.Slug.Trim() : SlugGenerator.Generate(row.Name);
                existing ??= productsBySlug.GetValueOrDefault(slug);
                if (slug.Length == 0 || SlugGenerator.Generate(slug) != slug)
                {
                    return "slug yalnız küçük harf, rakam ve tire içerebilir.";
                }

                if (!slugsInFile.Add(slug))
                {
                    return $"slug '{slug}' tabloda iki kez ürün satırı olarak geçiyor.";
                }

                if (productsBySlug.TryGetValue(slug, out var owner) && owner.Id != existing?.Id)
                {
                    return $"slug '{slug}' başka bir ürünün.";
                }

                if (row.Name.Trim().Length is 0 or > 200)
                {
                    return "ad zorunlu (en çok 200 karakter).";
                }

                if (!categoriesBySlug.TryGetValue(row.CategorySlug.Trim(), out var category))
                {
                    return $"Kategori bulunamadı: {row.CategorySlug.Trim()}";
                }

                if (Decimal(row.Price) is not { } price || price < 0m)
                {
                    return $"fiyat sayı olmalı ve eksi olamaz ('{row.Price}').";
                }

                if (row.CampaignPrice.Trim().Length > 0 && (Decimal(row.CampaignPrice) is not { } campaign || campaign < 0m || campaign >= price))
                {
                    return "kampanya fiyatı fiyattan küçük bir sayı olmalı.";
                }

                if (row.CampaignEndsAt.Trim().Length > 0 && Date(row.CampaignEndsAt) is null)
                {
                    return "kampanya bitişi yyyy-AA-gg ya da yyyy-AA-gg SS:dd olmalı.";
                }

                if (row.Stock.Trim().Length > 0 && Integer(row.Stock) is not >= 0)
                {
                    return "stok 0 ya da daha büyük tam sayı olmalı.";
                }

                if (row.IsActive.Trim().Length > 0 && !IsFlag(row.IsActive))
                {
                    return "yayinda 1/0 (evet/hayır) olmalı.";
                }

                if (AttributeText.Parse(row.Attributes).Problem is { } attributeProblem)
                {
                    return "ozellikler: " + attributeProblem;
                }

                var root = category.ParentId is { } parentId ? categories.FirstOrDefault(c => c.Id == parentId) ?? category : category;
                var hasVariants = existing is not null && productsWithVariants.Contains(existing.Id) || variantSlugsInFile.Contains(slug);
                if (row.Stock.Trim().Length > 0 && (hasVariants || ProductRules.RequiresVariants(root)))
                {
                    return "varyantlı üründe (ve Giyim'de) stok varyantta tutulur; ürün stok hücresi boş kalmalı.";
                }

                if (Flag(row.IsActive) && ProductRules.RequiresVariants(root) && !hasVariants)
                {
                    return "Giyim ürünü en az bir varyant olmadan yayına alınamaz.";
                }

                var urls = row.Images.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (urls.FirstOrDefault(u => !ImageAllowed(u)) is { } badUrl)
                {
                    return $"görsel adresi izinli değil: {badUrl} (yerel /… ya da Shop:ImageOrigins kökeni).";
                }

                step = new Step(
                    row,
                    new ImportRowResult(row.RowNumber, existing is null ? ImportRowKind.Yeni : ImportRowKind.Guncelle, slug, null),
                    slug,
                    category.Id,
                    existing?.Id,
                    null,
                    urls.Length == 0 ? null : urls);
                return null;
            }

            var problem = Problem(out var planned);
            plan.Add(problem is null ? planned : planned with { Result = planned.Result with { Reason = problem } });
        }

        foreach (var row in rows.Where(r => r.IsVariant))
        {
            var slug = row.Slug.Trim();
            var sku = row.Sku.Trim();
            var inFile = plan.FirstOrDefault(p => !p.Row.IsVariant && p.Slug == slug && p.Result.Kind != ImportRowKind.Hata);
            var product = productsBySlug.GetValueOrDefault(slug);
            var variant = variantsBySku.GetValueOrDefault(sku);

            string? problem = null;
            if (inFile is null && product is null)
            {
                problem = $"slug '{slug}' ile ürün yok (tabloda da geçerli ürün satırı yok).";
            }
            else if (sku.Length > 60 || !skusInFile.Add(sku))
            {
                problem = $"stok kodu '{sku}' tabloda iki kez geçiyor ya da 60 karakteri aşıyor.";
            }
            else if (variant is not null && variant.ProductId != (inFile?.ExistingProductId ?? product?.Id))
            {
                problem = $"stok kodu '{sku}' başka bir ürünün varyantı.";
            }
            else if (Integer(row.VariantStock) is not >= 0)
            {
                problem = "varyant_stok 0 ya da daha büyük tam sayı olmalı.";
            }
            else if (inFile is null && product!.Stock is not null)
            {
                problem = "ürünün kendi stoğu dolu; önce ürün satırında stok hücresini boşaltın.";
            }

            plan.Add(new Step(
                row,
                new ImportRowResult(row.RowNumber, problem is not null ? ImportRowKind.Hata : variant is null ? ImportRowKind.Yeni : ImportRowKind.Guncelle, sku, problem),
                slug,
                0,
                inFile?.ExistingProductId ?? product?.Id,
                variant?.Id,
                null));
        }

        return plan.OrderBy(p => p.Row.RowNumber).ToList();
    }

    /// <summary>Listeden çıkan görsellerin adreslerini döner; dosyalarını silmek çağıranın işi (depo Web katmanında).</summary>
    private async Task<List<string>> ReplaceImagesAsync(Product product, IReadOnlyList<string> urls, CancellationToken cancellationToken)
    {
        var current = await _imageDal.GetListAsync(i => i.ProductId == product.Id, cancellationToken);
        var removed = new List<string>();
        foreach (var stale in current.Where(i => !urls.Contains(i.Url)))
        {
            _imageDal.Delete((await _imageDal.GetTrackedAsync(i => i.Id == stale.Id, cancellationToken))!);
            removed.Add(stale.Url);
        }

        for (var index = 0; index < urls.Count; index++)
        {
            var existing = current.FirstOrDefault(i => i.Url == urls[index]);
            var image = existing is null
                ? new ProductImage { ProductId = product.Id, Url = urls[index], Alt = product.Name }
                : (await _imageDal.GetTrackedAsync(i => i.Id == existing.Id, cancellationToken))!;
            image.SortOrder = index;
            image.IsPrimary = index == 0;
            if (existing is null)
            {
                await _imageDal.AddAsync(image, cancellationToken);
            }
        }

        return removed;
    }

    private bool ImageAllowed(string url)
        => ProductRules.IsAllowedImageUrl(url)
           && (url.StartsWith('/') || _shop.ImageOrigins.Any(origin =>
               Uri.TryCreate(url, UriKind.Absolute, out var absolute)
               && string.Equals(absolute.GetLeftPart(UriPartial.Authority), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)));

    private static string Label(ProductSheetRow row) => row.Slug.Trim().Length > 0 ? row.Slug.Trim() : row.Name.Trim();

    private static string Text(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Blank(string value) => value.Trim().Length == 0 ? null : value.Trim();

    private static decimal? Decimal(string value)
        => decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static int? Integer(string value)
        => int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static DateTime? Date(string value)
        => DateTime.TryParseExact(value.Trim(), ["yyyy-MM-dd HH:mm", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static bool IsFlag(string value) => value.Trim().ToLowerInvariant() is "1" or "0" or "evet" or "hayır" or "hayir" or "true" or "false";

    private static bool Flag(string value) => value.Trim().ToLowerInvariant() is "1" or "evet" or "true";

    /// <summary>Slug/CategoryId/ExistingProductId ürün satırında; varyant satırında Slug ürünü, ExistingVariantId stok kodunu gösterir.</summary>
    private sealed record Step(
        ProductSheetRow Row,
        ImportRowResult Result,
        string Slug = "",
        int CategoryId = 0,
        int? ExistingProductId = null,
        int? ExistingVariantId = null,
        IReadOnlyList<string>? Images = null);
}
