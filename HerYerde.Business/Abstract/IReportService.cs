using HerYerde.Business.Dtos;

namespace HerYerde.Business.Abstract;

public interface IReportService
{
    /// <summary>[fromUtc, toUtc) aralığında açılan siparişlerin özeti; dönemler ve günler <paramref name="zone"/> saatine göre.</summary>
    Task<SalesReport> BuildAsync(DateTime fromUtc, DateTime toUtc, ReportPeriod period, TimeZoneInfo zone, CancellationToken cancellationToken = default);
}
