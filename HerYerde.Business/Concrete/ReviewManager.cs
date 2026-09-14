using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class ReviewManager : IReviewService
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int Window = 200;

    private readonly IProductReviewDal _reviewDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ReviewManager(IProductReviewDal reviewDal, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _reviewDal = reviewDal;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IResult)> AddAsync(ProductReview review, CancellationToken cancellationToken = default)
    {
        if (review.Rating is < MinRating or > MaxRating)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Puan 1 ile 5 arasında olmalı."));
        }

        var orderNo = review.OrderNo?.Trim();
        review.OrderNo = orderNo is { Length: > 0 } && await _reviewDal.IsPurchaseAsync(orderNo, review.ProductId, cancellationToken)
            ? orderNo
            : null;
        review.IsApproved = false;
        review.CreatedAt = _clock.GetUtcNow().UtcDateTime;

        await _reviewDal.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Yorumunuz onaylandıktan sonra yayınlanır."));
    }

    public async Task<(HttpStatusCode, IDataResult<List<ProductReview>>)> GetApprovedAsync(int productId, CancellationToken cancellationToken = default)
        => (HttpStatusCode.OK, new SuccessDataResult<List<ProductReview>>(await _reviewDal.GetApprovedAsync(productId, cancellationToken)));

    public async Task<(HttpStatusCode, IDataResult<List<ReviewRow>>)> GetLatestApprovedAsync(int take, CancellationToken cancellationToken = default)
        => (HttpStatusCode.OK, new SuccessDataResult<List<ReviewRow>>(await _reviewDal.GetLatestApprovedAsync(take, cancellationToken)));

    public async Task<(HttpStatusCode, IDataResult<List<ReviewRow>>)> GetForAdminAsync(CancellationToken cancellationToken = default)
        => (HttpStatusCode.OK, new SuccessDataResult<List<ReviewRow>>(await _reviewDal.GetForAdminAsync(Window, cancellationToken)));

    public async Task<(HttpStatusCode, IResult)> ApproveAsync(int id, CancellationToken cancellationToken = default)
    {
        var review = await _reviewDal.GetTrackedAsync(r => r.Id == id, cancellationToken);
        if (review is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Yorum bulunamadı."));
        }

        review.IsApproved = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Yorum yayınlandı."));
    }

    public async Task<(HttpStatusCode, IResult)> RejectAsync(int id, CancellationToken cancellationToken = default)
    {
        var review = await _reviewDal.GetTrackedAsync(r => r.Id == id, cancellationToken);
        if (review is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Yorum bulunamadı."));
        }

        _reviewDal.Delete(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Yorum reddedildi."));
    }
}
