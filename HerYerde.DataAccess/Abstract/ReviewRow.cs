using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

/// <summary>Yorum ve ait olduğu ürünün adı/slug'ı; yönetim listesi ve ana sayfa alıntıları için.</summary>
public sealed record ReviewRow(ProductReview Review, string ProductName, string ProductSlug);
