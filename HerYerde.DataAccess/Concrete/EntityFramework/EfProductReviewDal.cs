using HerYerde.Core.DataAccess.EntityFramework;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

public class EfProductReviewDal : EfEntityRepositoryBase<ProductReview, HerYerdeContext>, IProductReviewDal
{
    public EfProductReviewDal(HerYerdeContext context) : base(context)
    {
    }

    public Task<List<ProductReview>> GetApprovedAsync(int productId, CancellationToken cancellationToken = default)
        => Context.ProductReviews.AsNoTracking()
            .Where(r => r.ProductId == productId && r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

    public Task<List<ReviewRow>> GetLatestApprovedAsync(int take, CancellationToken cancellationToken = default)
        => (from review in Context.ProductReviews.AsNoTracking()
            join product in Context.Products on review.ProductId equals product.Id
            where review.IsApproved && product.IsActive
            orderby review.CreatedAt descending, review.Id descending
            select new ReviewRow(review, product.Name, product.Slug))
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<List<ReviewRow>> GetForAdminAsync(int take, CancellationToken cancellationToken = default)
        => (from review in Context.ProductReviews.AsNoTracking()
            join product in Context.Products.IgnoreQueryFilters() on review.ProductId equals product.Id
            orderby review.IsApproved, review.CreatedAt descending, review.Id descending
            select new ReviewRow(review, product.Name, product.Slug))
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<List<ReviewRow>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        => (from review in Context.ProductReviews.AsNoTracking()
            join product in Context.Products.IgnoreQueryFilters() on review.ProductId equals product.Id
            where review.CustomerId == customerId
            orderby review.CreatedAt descending, review.Id descending
            select new ReviewRow(review, product.Name, product.Slug))
            .ToListAsync(cancellationToken);

    public Task<bool> IsPurchaseAsync(string orderNo, int productId, CancellationToken cancellationToken = default)
        => (from order in Context.Orders
            join item in Context.OrderItems on order.Id equals item.OrderId
            where order.OrderNo == orderNo && order.Status != OrderStatus.IptalEdildi
            select item.Sku)
            .AnyAsync(
                sku => Context.Products.Any(p => p.Id == productId && p.Slug == sku)
                       || Context.ProductVariants.Any(v => v.ProductId == productId && v.Sku == sku),
                cancellationToken);
}
