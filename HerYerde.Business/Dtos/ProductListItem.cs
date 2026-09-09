using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Dtos;

/// <summary>Vitrin listeleri için ürün + kendi kategorisinin slug'ı; kategori tablosuna ikinci sorgu gerekmez.</summary>
public sealed record ProductListItem(Product Product, string CategorySlug);
