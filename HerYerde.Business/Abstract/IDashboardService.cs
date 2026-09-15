using HerYerde.Business.Dtos;

namespace HerYerde.Business.Abstract;

public interface IDashboardService
{
    /// <summary>Gün ve hafta (pazartesi) <paramref name="zone"/> saatine göre; sayaçlar tek sorguda, düşük stok ve gecikmiş kargo ayrı sorgularda.</summary>
    Task<DashboardView> GetAsync(TimeZoneInfo zone, CancellationToken cancellationToken = default);
}
