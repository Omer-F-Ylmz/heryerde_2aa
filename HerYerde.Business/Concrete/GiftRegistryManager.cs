using System.Net;
using System.Security.Cryptography;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class GiftRegistryManager : IGiftRegistryService
{
    public const int SlugLength = 10;

    /// <summary>Karışan harf/rakam yok (0/o, 1/l): adres elle yazılıp WhatsApp'tan okunabilsin.</summary>
    private const string SlugAlphabet = "abcdefghijkmnpqrstuvwxyz23456789";

    private readonly IGiftRegistryDal _registryDal;
    private readonly IGiftRegistryItemDal _itemDal;
    private readonly ICartItemDal _cartItemDal;
    private readonly IProductDal _productDal;
    private readonly IProductVariantDal _variantDal;
    private readonly IProductImageDal _imageDal;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public GiftRegistryManager(
        IGiftRegistryDal registryDal,
        IGiftRegistryItemDal itemDal,
        ICartItemDal cartItemDal,
        IProductDal productDal,
        IProductVariantDal variantDal,
        IProductImageDal imageDal,
        INotificationService notifications,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _registryDal = registryDal;
        _itemDal = itemDal;
        _cartItemDal = cartItemDal;
        _productDal = productDal;
        _variantDal = variantDal;
        _imageDal = imageDal;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IDataResult<GiftRegistry>)> CreateAsync(GiftRegistryDraft draft, CancellationToken cancellationToken = default)
    {
        if (!PhoneRules.TryNormalize(draft.Phone, out var phone))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<GiftRegistry>("Geçerli bir cep telefonu yazın (05XX XXX XX XX)."));
        }

        if (DraftProblem(draft) is { } problem)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<GiftRegistry>(problem));
        }

        var registry = new GiftRegistry
        {
            Slug = await NewSlugAsync(cancellationToken),
            ManageToken = Guid.NewGuid(),
            OwnerName = draft.OwnerName.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(draft.Email) ? null : draft.Email.Trim(),
            EventDate = draft.EventDate.Date,
            Message = string.IsNullOrWhiteSpace(draft.Message) ? null : draft.Message.Trim(),
            IsPublic = true,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };

        await _registryDal.AddAsync(registry, cancellationToken);
        await _notifications.QueueGiftRegistryCreatedAsync(registry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessDataResult<GiftRegistry>(registry, "Çeyiz listeniz açıldı."));
    }

    public async Task<GiftRegistryView?> GetPublicAsync(string slug, CancellationToken cancellationToken = default)
        => await _registryDal.GetAsync(r => r.Slug == slug && r.IsPublic, cancellationToken) is { } registry
            ? await ViewAsync(registry, cancellationToken)
            : null;

    public async Task<GiftRegistryView?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default)
        => await _registryDal.GetAsync(r => r.ManageToken == token, cancellationToken) is { } registry
            ? await ViewAsync(registry, cancellationToken)
            : null;

    public async Task<(HttpStatusCode, IResult)> UpdateAsync(Guid token, GiftRegistryDraft draft, CancellationToken cancellationToken = default)
    {
        var registry = await _registryDal.GetTrackedAsync(r => r.ManageToken == token, cancellationToken);
        if (registry is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Liste bulunamadı."));
        }

        if (DraftProblem(draft) is { } problem)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(problem));
        }

        registry.OwnerName = draft.OwnerName.Trim();
        registry.EventDate = draft.EventDate.Date;
        registry.Message = string.IsNullOrWhiteSpace(draft.Message) ? null : draft.Message.Trim();
        registry.IsPublic = draft.IsPublic;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Liste güncellendi."));
    }

    public async Task<(HttpStatusCode, IResult)> AddItemAsync(Guid token, int productId, int? variantId, int desiredQty, CancellationToken cancellationToken = default)
    {
        var registry = await _registryDal.GetAsync(r => r.ManageToken == token, cancellationToken);
        if (registry is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Liste bulunamadı."));
        }

        if (desiredQty is < 1 or > 99)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Adet 1 ile 99 arasında olmalı."));
        }

        if (await _productDal.GetAsync(p => p.Id == productId && p.IsActive, cancellationToken) is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Ürün bulunamadı."));
        }

        var hasVariants = await _variantDal.GetAsync(v => v.ProductId == productId, cancellationToken) is not null;
        if (hasVariants && (variantId is null
                            || await _variantDal.GetAsync(v => v.Id == variantId && v.ProductId == productId, cancellationToken) is null))
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Beden ya da renk seçin."));
        }

        var chosen = hasVariants ? variantId : null;
        var existing = await _itemDal.GetTrackedAsync(
            i => i.GiftRegistryId == registry.Id && i.ProductId == productId && i.VariantId == chosen,
            cancellationToken);

        if (existing is null)
        {
            await _itemDal.AddAsync(new GiftRegistryItem
            {
                GiftRegistryId = registry.Id,
                ProductId = productId,
                VariantId = chosen,
                DesiredQty = desiredQty
            }, cancellationToken);
        }
        else
        {
            existing.DesiredQty = Math.Min(99, existing.DesiredQty + desiredQty);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Ürün listeye eklendi."));
    }

    public async Task<(HttpStatusCode, IResult)> SetItemQuantityAsync(Guid token, int itemId, int desiredQty, CancellationToken cancellationToken = default)
    {
        if (desiredQty is < 1 or > 99)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Adet 1 ile 99 arasında olmalı."));
        }

        var item = await OwnedItemAsync(token, itemId, cancellationToken);
        if (item is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Liste kalemi bulunamadı."));
        }

        item.DesiredQty = desiredQty;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Adet güncellendi."));
    }

    public async Task<(HttpStatusCode, IResult)> RemoveItemAsync(Guid token, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await OwnedItemAsync(token, itemId, cancellationToken);
        if (item is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Liste kalemi bulunamadı."));
        }

        await DeleteCartLinesAsync([item.Id], cancellationToken);
        _itemDal.Delete(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Ürün listeden çıkarıldı."));
    }

    public async Task<List<GiftRegistrySummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var registries = await _registryDal.GetListAsync(cancellationToken: cancellationToken);
        var items = await _itemDal.GetListAsync(cancellationToken: cancellationToken);
        return registries
            .OrderByDescending(r => r.CreatedAt)
            .Select(r =>
            {
                var own = items.Where(i => i.GiftRegistryId == r.Id).ToList();
                return new GiftRegistrySummary(
                    r,
                    own.Count,
                    own.Sum(i => i.DesiredQty),
                    own.Sum(i => Math.Min(i.ReceivedQty, i.DesiredQty)));
            })
            .ToList();
    }

    public async Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var registry = await _registryDal.GetTrackedAsync(r => r.Id == id, cancellationToken);
        if (registry is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Liste bulunamadı."));
        }

        await RemoveAsync([registry], cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Liste silindi."));
    }

    public async Task<bool> IsOwnerAsync(IReadOnlyCollection<int> itemIds, string phone, string? email, CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
        {
            return false;
        }

        var registryIds = (await _itemDal.GetListAsync(i => itemIds.Contains(i.Id), cancellationToken))
            .Select(i => i.GiftRegistryId)
            .Distinct()
            .ToList();
        var registries = await _registryDal.GetListAsync(r => registryIds.Contains(r.Id), cancellationToken);
        var mail = email?.Trim();

        return registries.Any(r => r.Phone == phone
                                   || mail is { Length: > 0 } && string.Equals(r.Email, mail, StringComparison.OrdinalIgnoreCase));
    }

    public async Task RecordPurchaseAsync(Order order, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken = default)
    {
        var gifted = items.Where(i => i.GiftRegistryItemId is not null && !i.IsGift).ToList();
        if (gifted.Count == 0)
        {
            return;
        }

        foreach (var line in gifted)
        {
            await _itemDal.IncrementReceivedAsync(line.GiftRegistryItemId!.Value, line.Quantity, cancellationToken);
        }

        var itemIds = gifted.Select(i => i.GiftRegistryItemId!.Value).Distinct().ToList();
        var registryOf = (await _itemDal.GetListAsync(i => itemIds.Contains(i.Id), cancellationToken))
            .ToDictionary(i => i.Id, i => i.GiftRegistryId);
        var registryIds = registryOf.Values.Distinct().ToList();

        // Liste siparişten önce silinmiş olabilir: o satırlar sessizce atlanır.
        foreach (var registry in await _registryDal.GetListAsync(r => registryIds.Contains(r.Id), cancellationToken))
        {
            var lines = gifted.Where(i => registryOf.GetValueOrDefault(i.GiftRegistryItemId!.Value) == registry.Id).ToList();
            await _notifications.QueueGiftRegistryPurchaseAsync(registry, order, lines, cancellationToken);
        }
    }

    public async Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default)
    {
        var limit = _clock.GetUtcNow().UtcDateTime.Date - age;
        var expired = await _registryDal.GetListAsync(r => r.EventDate < limit, cancellationToken);
        if (expired.Count == 0)
        {
            return 0;
        }

        await RemoveAsync(expired, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private static string? DraftProblem(GiftRegistryDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.OwnerName) || draft.OwnerName.Trim().Length > 60)
        {
            return "Adınızı yazın (en çok 60 karakter).";
        }

        return draft.Message?.Trim().Length > 500 ? "Mesaj en çok 500 karakter olabilir." : null;
    }

    private async Task<string> NewSlugAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var slug = new string(RandomNumberGenerator.GetItems<char>(SlugAlphabet, SlugLength));
            if (await _registryDal.GetAsync(r => r.Slug == slug, cancellationToken) is null)
            {
                return slug;
            }
        }
    }

    private async Task<GiftRegistryItem?> OwnedItemAsync(Guid token, int itemId, CancellationToken cancellationToken)
    {
        var registry = await _registryDal.GetAsync(r => r.ManageToken == token, cancellationToken);
        return registry is null
            ? null
            : await _itemDal.GetTrackedAsync(i => i.Id == itemId && i.GiftRegistryId == registry.Id, cancellationToken);
    }

    private async Task<GiftRegistryView> ViewAsync(GiftRegistry registry, CancellationToken cancellationToken)
    {
        var items = await _itemDal.GetListAsync(i => i.GiftRegistryId == registry.Id, cancellationToken);
        if (items.Count == 0)
        {
            return new GiftRegistryView(registry, []);
        }

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await _productDal.GetListAsync(p => productIds.Contains(p.Id), cancellationToken)).ToDictionary(p => p.Id);
        var variantIds = items.Where(i => i.VariantId is not null).Select(i => i.VariantId!.Value).ToList();
        var variants = variantIds.Count == 0
            ? []
            : (await _variantDal.GetListAsync(v => variantIds.Contains(v.Id), cancellationToken)).ToDictionary(v => v.Id);
        var images = await _imageDal.GetListAsync(i => productIds.Contains(i.ProductId), cancellationToken);

        var lines = new List<GiftRegistryLine>();
        foreach (var item in items)
        {
            // Ürün silindiyse (yumuşak silme süzgeci) satır listeden düşer; alınan adet kaydı kalemde durur.
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                continue;
            }

            var variant = item.VariantId is { } id ? variants.GetValueOrDefault(id) : null;
            lines.Add(new GiftRegistryLine(
                item.Id,
                product.Id,
                product.Name,
                product.Slug,
                images.Where(i => i.ProductId == product.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).FirstOrDefault()?.Url,
                variant?.Size,
                variant?.Color,
                item.DesiredQty,
                item.ReceivedQty,
                product.IsActive && (item.VariantId is null || variant is not null)));
        }

        // Tamamlananlar sona: ziyaretçi önce alınabilecekleri görür.
        return new GiftRegistryView(registry, lines.OrderBy(l => l.Completed).ThenBy(l => l.ItemId).ToList());
    }

    private async Task RemoveAsync(IReadOnlyList<GiftRegistry> registries, CancellationToken cancellationToken)
    {
        var registryIds = registries.Select(r => r.Id).ToList();
        var items = await _itemDal.GetListAsync(i => registryIds.Contains(i.GiftRegistryId), cancellationToken);
        await DeleteCartLinesAsync(items.Select(i => i.Id).ToList(), cancellationToken);

        foreach (var item in items)
        {
            _itemDal.Delete(item);
        }

        foreach (var registry in registries)
        {
            _registryDal.Delete(registry);
        }
    }

    /// <summary>Sepet satırı kaleme yabancı anahtarla bağlı ve basamaklı silme yok: önce satırlar gider.</summary>
    private async Task DeleteCartLinesAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        foreach (var line in await _cartItemDal.GetListAsync(i => i.GiftRegistryItemId != null && itemIds.Contains(i.GiftRegistryItemId.Value), cancellationToken))
        {
            _cartItemDal.Delete(line);
        }
    }
}
