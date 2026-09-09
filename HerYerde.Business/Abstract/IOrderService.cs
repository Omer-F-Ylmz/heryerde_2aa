using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Abstract;

public interface IOrderService
{
    /// <summary>Sepeti siparişe çevirir: stok düşer, sepet boşalır. Stok yetmezse 409 ile hiçbiri olmaz.</summary>
    Task<(HttpStatusCode, IDataResult<Order>)> PlaceAsync(Guid cartId, OrderDraft draft, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<OrderDetail>)> GetByOrderNoAsync(string orderNo, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<OrderDetail>)> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Yönetim listesi: duruma göre süzer, sipariş numarası veya telefonla arar.</summary>
    Task<(HttpStatusCode, IDataResult<List<Order>>)> SearchAsync(OrderStatus? status, string? query, CancellationToken cancellationToken = default);

    /// <summary>Yalnız <see cref="Rules.OrderRules.CanTransition"/> izin verirse; aksi halde 400.</summary>
    Task<(HttpStatusCode, IResult)> ChangeStatusAsync(int orderId, OrderStatus next, CancellationToken cancellationToken = default);
}
