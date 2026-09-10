using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IProductDal : IEntityRepository<Product>
{
    /// <summary>Yayındaki ürünler, kendi kategorilerinin slug'ıyla; süzme/sıralama/sayfalama ve
    /// süzgece uyan toplam sayı tek sorguda.</summary>
    Task<(List<(Product Product, string CategorySlug)> Items, int Total)> GetActiveAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Ana sayfa hero'su: kampanyası süren, en yakın biten ürün.</summary>
    Task<(Product Product, string CategorySlug)?> GetCampaignHeroAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Yönetim listesi: ürün kimliği başına varyant stok toplamı, tek gruplu sorguda.</summary>
    Task<Dictionary<int, int>> GetStockTotalsAsync(CancellationToken cancellationToken = default);

    /// <summary>Slug benzersizliği soft-delete süzgecini yok sayar: silinen ürünün slug'ı da doludur.</summary>
    Task<bool> SlugTakenAsync(string slug, int excludedId, CancellationToken cancellationToken = default);
}
