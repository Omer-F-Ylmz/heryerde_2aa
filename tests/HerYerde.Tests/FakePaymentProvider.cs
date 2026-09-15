using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;

namespace HerYerde.Tests;

/// <summary>Testlerde İyzico'ya ağ çağrısı yok; başlatılan ödemeler burada birikir, sonuçlar ayarlanır.</summary>
public sealed class FakePaymentProvider : IPaymentProvider
{
    public const string FormAction = "https://sandbox-api.iyzipay.com/payment/mock/init3ds";

    public string Name => "iyzico";

    public bool InitFails { get; set; }

    public bool AuthFails { get; set; }

    public bool SignatureValid { get; set; } = true;

    /// <summary>Doluysa çekim yanıtındaki tutar sipariş toplamı yerine bu olur.</summary>
    public decimal? PaidPrice { get; set; }

    public List<PaymentInitRequest> Inits { get; } = [];

    public int AuthCalls { get; private set; }

    public bool RefundFails { get; set; }

    public List<PaymentRefundRequest> Refunds { get; } = [];

    public Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        if (RefundFails)
        {
            return Task.FromResult(new PaymentRefundResult(false, "İade reddedildi.", "{\"status\":\"failure\"}"));
        }

        Refunds.Add(request);
        return Task.FromResult(new PaymentRefundResult(true, null, "{\"status\":\"success\"}"));
    }

    public Task<PaymentInitResult> InitThreeDsAsync(PaymentInitRequest request, CancellationToken cancellationToken = default)
    {
        Inits.Add(request);
        // Gerçek sağlayıcının kart numarasını yankılaması en kötü durum: saklanan yanıtta görünmemeli.
        var raw = $"{{\"status\":\"{(InitFails ? "failure" : "success")}\",\"cardNumber\":\"{request.Card.Number}\",\"cvc\":\"{request.Card.Cvc}\"}}";
        return Task.FromResult(InitFails
            ? new PaymentInitResult(false, null, null, "Kart limiti yetersiz.", raw)
            : new PaymentInitResult(
                true,
                "pay-" + Inits.Count,
                new ThreeDsForm(FormAction, new Dictionary<string, string> { ["conversationId"] = request.ConversationId }),
                null,
                raw));
    }

    public bool IsValidCallback(PaymentCallback callback) => SignatureValid;

    public Task<PaymentAuthResult> CompleteThreeDsAsync(PaymentCallback callback, CancellationToken cancellationToken = default)
    {
        AuthCalls++;
        var total = Inits.Last(i => i.ConversationId == callback.ConversationId).Order.Total;
        return Task.FromResult(AuthFails
            ? new PaymentAuthResult(false, callback.PaymentId, 0m, "Banka çekimi reddetti.", "{\"status\":\"failure\"}")
            : new PaymentAuthResult(true, callback.PaymentId, PaidPrice ?? total, null, "{\"status\":\"success\",\"paymentId\":\"" + callback.PaymentId + "\"}"));
    }
}
