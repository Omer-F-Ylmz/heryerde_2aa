namespace HerYerde.Business.Dtos;

public sealed record AnalyticsDayRow(DateOnly Day, int Views, int Visits);

/// <summary>Label: yol (ürün, kategori), kaynak (boş = doğrudan/site içi) ya da cihaz sınıfı.</summary>
public sealed record AnalyticsCount(string Label, int Count);

/// <summary>Huni: ürün sayfası görüntülenmesi → sepete ekleme → ödeme adımı → sipariş.</summary>
public sealed record AnalyticsFunnel(int ProductViews, int AddToCart, int Checkout, int Orders);

/// <summary>Yönetim analitik raporu; günler yalnız kaydı olan günlerdir, sıralı listeler en çoktan aza (eşitlikte ada göre).</summary>
public sealed record AnalyticsReport(
    DateOnly From,
    DateOnly To,
    int TotalViews,
    int TotalVisits,
    IReadOnlyList<AnalyticsDayRow> Days,
    IReadOnlyList<AnalyticsCount> TopProducts,
    IReadOnlyList<AnalyticsCount> TopCategories,
    IReadOnlyList<AnalyticsCount> Sources,
    IReadOnlyList<AnalyticsCount> Devices,
    AnalyticsFunnel Funnel,
    int WhatsAppClicks,
    int InstagramClicks,
    int Searches);
