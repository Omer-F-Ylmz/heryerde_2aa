using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Dtos;

/// <summary>KVKK erişim dökümü: kişinin tüm tablolardaki kayıtları.</summary>
public sealed record KvkkPerson(
    string Subject,
    List<Order> Orders,
    List<OrderItem> OrderItems,
    List<Payment> Payments,
    List<PaymentNotice> PaymentNotices,
    List<ReturnRequest> ReturnRequests,
    List<ReturnRequestItem> ReturnRequestItems,
    List<ContactMessage> ContactMessages,
    List<ProductReview> Reviews,
    KvkkAccount? Account,
    List<CustomerAddress> Addresses);

/// <summary>Tümünü anonimleştir sonucu; Files silinecek gizli belgeler (çağıranın işi), açık siparişler atlanır.</summary>
public sealed record KvkkAnonymizeResult(
    IReadOnlyList<Order> AnonymizedOrders,
    IReadOnlyList<string> SkippedOrderNos,
    int DeletedMessages,
    int MaskedReviews,
    IReadOnlyList<string> Files);

/// <summary>Başvuru ve kalan gün (eksi: süre geçti; yanıtlanmışsa null).</summary>
public sealed record KvkkRequestRow(KvkkRequest Request, int? DaysLeft);

/// <summary>Yönetim panosu: ciro rapor kuralıyla (kasaya giren), sipariş sayısı iptal hariç.</summary>
public sealed record DashboardView(
    int TodayOrders,
    decimal TodayRevenue,
    int WeekOrders,
    decimal WeekRevenue,
    int PendingNotices,
    int PendingReturns,
    int PendingReviews,
    int LowStock,
    int UnreadMessages,
    IReadOnlyList<Order> LateDispatch);

/// <summary>Sipariş zaman çizelgesi satırı. Kind: siparis, posta, islem, not, iade.</summary>
public sealed record TimelineEntry(DateTime At, string Kind, string Title, string? Detail, string? Actor);
