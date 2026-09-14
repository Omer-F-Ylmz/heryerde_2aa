using System.Net;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IReviewService
{
    /// <summary>Yorumu onaysız kaydeder; puan 1-5 dışıysa 400. Sipariş numarası bu ürünü içeren iptal edilmemiş bir
    /// siparişe ait değilse düşürülür: yorum yine alınır, doğrulanmış alıcı rozeti verilmez.</summary>
    Task<(HttpStatusCode, IResult)> AddAsync(ProductReview review, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<List<ProductReview>>)> GetApprovedAsync(int productId, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<List<ReviewRow>>)> GetLatestApprovedAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>Son 200 yorum; onay bekleyenler önce.</summary>
    Task<(HttpStatusCode, IDataResult<List<ReviewRow>>)> GetForAdminAsync(CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> ApproveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Ret kaydı siler.</summary>
    Task<(HttpStatusCode, IResult)> RejectAsync(int id, CancellationToken cancellationToken = default);
}
