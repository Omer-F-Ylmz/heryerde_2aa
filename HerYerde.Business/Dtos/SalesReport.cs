using HerYerde.Entities.Enums;

namespace HerYerde.Business.Dtos;

public enum ReportPeriod
{
    Gun = 1,
    Hafta = 2,
    Ay = 3
}

/// <summary>Tarih aralığının satış özeti. Ciro yalnız parası alınmış siparişlerden (bkz. ReportManager); sipariş sayısı
/// aralıkta açılan tümü; iptal oranı iptal / tümü.</summary>
public sealed record SalesReport(
    decimal Revenue,
    int OrderCount,
    int PaidOrderCount,
    decimal AverageBasket,
    decimal CancelRate,
    decimal Discount,
    IReadOnlyList<ReportPeriodRow> Periods,
    IReadOnlyList<TopProductRow> TopProducts,
    IReadOnlyList<SourceRow> Sources,
    IReadOnlyList<PaymentMethodRow> PaymentMethods);

/// <summary>Start: dönemin yerel (İstanbul) ilk günü.</summary>
public sealed record ReportPeriodRow(DateOnly Start, decimal Revenue, int Orders);

public sealed record TopProductRow(string ProductName, string Sku, int Quantity, decimal Amount);

public sealed record SourceRow(OrderSource Source, int Orders, decimal Revenue);

public sealed record PaymentMethodRow(PaymentMethod Method, int Orders, decimal Revenue);
