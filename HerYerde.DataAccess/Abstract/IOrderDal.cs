using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.DataAccess.Abstract;

public interface IOrderDal : IEntityRepository<Order>
{
    /// <summary>Sipariş numarasının sıra kısmını veritabanı SEQUENCE'ından alır; eşzamanlı isteklerde
    /// her çağrı farklı değer döner, sıra gün başında sıfırlanmaz.</summary>
    Task<long> NextOrderSequenceAsync(CancellationToken cancellationToken = default);

    /// <summary>Siparişi yalnız hâlâ <paramref name="from"/> durumundaysa <paramref name="to"/> yapar; tek koşullu UPDATE, eşzamanlı
    /// ikinci istek 0 alır (ör. müşteri iptalinde stok iki kez dönmesin).</summary>
    Task<int> TryChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to, CancellationToken cancellationToken = default);

    /// <summary>Yönetimde hiç açılmamış sipariş sayısı.</summary>
    Task<int> UnseenCountAsync(CancellationToken cancellationToken = default);
}
