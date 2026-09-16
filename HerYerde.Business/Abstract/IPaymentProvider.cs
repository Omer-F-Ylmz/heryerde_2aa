using HerYerde.Business.Dtos;

namespace HerYerde.Business.Abstract;

/// <summary>Sanal POS sağlayıcısı (İyzico). Ağ hatası istisna değil başarısız sonuç olarak döner.</summary>
public interface IPaymentProvider
{
    string Name { get; }

    /// <summary>Ödeme + 3D doğrulamayı başlatır; başarıda tarayıcıya verilecek formu döner.</summary>
    Task<PaymentInitResult> InitThreeDsAsync(PaymentInitRequest request, CancellationToken cancellationToken = default);

    /// <summary>Dönüş alanlarının imzası sağlayıcı gizli anahtarıyla tutuyor mu.</summary>
    bool IsValidCallback(PaymentCallback callback);

    /// <summary>3D doğrulaması geçen ödemeyi tamamlar (çekim).</summary>
    Task<PaymentAuthResult> CompleteThreeDsAsync(PaymentCallback callback, CancellationToken cancellationToken = default);

    /// <summary>Çekilmiş ödemenin tamamını geri verir.</summary>
    Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>Tutarın taksit tabloları. BIN (kart numarasının ilk 6 hanesi) verilirse o kartın bankasına göre,
    /// verilmezse banka başına ayrı tablo.</summary>
    Task<InstallmentResult> GetInstallmentsAsync(decimal price, string? bin, CancellationToken cancellationToken = default);
}
