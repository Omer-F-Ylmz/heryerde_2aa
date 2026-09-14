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
}
