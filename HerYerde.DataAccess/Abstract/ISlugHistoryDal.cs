using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface ISlugHistoryDal : IEntityRepository<SlugHistory>
{
    /// <summary>Eski slug'ı kayda bağlar (varsa satırı yeni sahibine çevirir) ve yeni slug'ı taşıyan eski satırı siler:
    /// bugün kullanılan adres yönlendirilmez. Kaydetmez; çağıranın SaveChanges'i yazar.</summary>
    Task RecordAsync(string entityType, int entityId, string oldSlug, string newSlug, DateTime at, CancellationToken cancellationToken = default);

    /// <summary>Eski slug'ın bugünkü sahibi; yoksa null.</summary>
    Task<int?> FindEntityIdAsync(string entityType, string oldSlug, CancellationToken cancellationToken = default);
}
