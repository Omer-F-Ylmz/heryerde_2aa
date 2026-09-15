using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Dtos;

/// <summary>Yalnız sağlayıcıya iletilmek üzere bellekte taşınır; loglanmaz, saklanmaz.</summary>
public sealed record PaymentCard(string HolderName, string Number, string ExpireMonth, string ExpireYear, string Cvc);

public sealed record PaymentInitRequest(
    Order Order,
    IReadOnlyList<OrderItem> Items,
    string ConversationId,
    PaymentCard Card,
    string BuyerIp,
    string CallbackUrl);

/// <summary>Tarayıcının bankaya otomatik göndereceği 3D doğrulama formu.</summary>
public sealed record ThreeDsForm(string Action, IReadOnlyDictionary<string, string> Fields);

public sealed record PaymentInitResult(bool Success, string? PaymentId, ThreeDsForm? Form, string? ErrorMessage, string RawResponse);

/// <summary>3D doğrulamadan sonra sağlayıcının tarayıcı üzerinden gönderdiği alanlar.</summary>
public sealed record PaymentCallback(
    string? Status,
    string? PaymentId,
    string? ConversationId,
    string? ConversationData,
    string? MdStatus,
    string? Signature);

/// <summary>Tam tutar iadede sağlayıcı önce iptal dener (aynı gün), olmazsa iade; Partial (kalem iadesi) ise iptal denenmez,
/// yalnız tutar kadar iade yapılır.</summary>
public sealed record PaymentRefundRequest(string PaymentId, string ConversationId, decimal Amount, string Ip, bool Partial = false);

public sealed record PaymentRefundResult(bool Success, string? ErrorMessage, string RawResponse);

public sealed record PaymentAuthResult(bool Success, string? PaymentId, decimal PaidPrice, string? ErrorMessage, string RawResponse);
