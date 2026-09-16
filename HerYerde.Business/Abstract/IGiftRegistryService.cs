using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

/// <summary>Çeyiz listesi. Sahip üye değildir: yönetim anahtarı (bağlantı) kimliğin yerine geçer.</summary>
public interface IGiftRegistryService
{
    /// <summary>Listeyi açar; e-posta verildiyse yönetim bağlantısı kuyruğa girer. Kaydeder.</summary>
    Task<(HttpStatusCode, IDataResult<GiftRegistry>)> CreateAsync(GiftRegistryDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Paylaşılan adres; liste yoksa ya da gizliyse null.</summary>
    Task<GiftRegistryView?> GetPublicAsync(string slug, CancellationToken cancellationToken = default);

    Task<GiftRegistryView?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default);

    /// <summary>Ad, tarih, mesaj ve paylaşım durumu. İletişim bilgisi değişmez: sahiplik denetimi ona dayanır.</summary>
    Task<(HttpStatusCode, IResult)> UpdateAsync(Guid token, GiftRegistryDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Ürün listede varsa istenen adet üzerine eklenir.</summary>
    Task<(HttpStatusCode, IResult)> AddItemAsync(Guid token, int productId, int? variantId, int desiredQty, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> SetItemQuantityAsync(Guid token, int itemId, int desiredQty, CancellationToken cancellationToken = default);

    /// <summary>Kalemi sepetlerdeki hediye satırlarıyla birlikte siler.</summary>
    Task<(HttpStatusCode, IResult)> RemoveItemAsync(Guid token, int itemId, CancellationToken cancellationToken = default);

    Task<List<GiftRegistrySummary>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Yönetimden silme: kalemler ve sepetlerdeki hediye satırları da gider.</summary>
    Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Kalemlerden biri, telefonu ya da e-postası sipariş verenle eşleşen birinin listesindeyse true.</summary>
    Task<bool> IsOwnerAsync(IReadOnlyCollection<int> itemIds, string phone, string? email, CancellationToken cancellationToken = default);

    /// <summary>Kesinleşen siparişin liste satırlarını alınan adede işler ve liste başına sahibe posta kuyruğa yazar.
    /// Artış tek UPDATE ile hemen yazılır; posta çağıranın kaydıyla gider.</summary>
    Task RecordPurchaseAsync(Order order, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken = default);

    /// <summary>Etkinlik tarihinin üzerinden verilen süre geçmiş listeleri siler; silinen liste sayısını döner.</summary>
    Task<int> PurgeOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default);
}
