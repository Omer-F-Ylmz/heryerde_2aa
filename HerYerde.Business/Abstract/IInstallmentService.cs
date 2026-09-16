using HerYerde.Business.Dtos;

namespace HerYerde.Business.Abstract;

/// <summary>Taksit tablosu; sağlayıcıya sorup bir saat önbellekler.</summary>
public interface IInstallmentService
{
    /// <summary>İyzico anahtarı ve Iyzico:Installments birlikte açıksa doğru; kapalıyken taksit hiç sorulmaz.</summary>
    bool Enabled { get; }

    /// <summary>Kapalıysa ya da sağlayıcı yanıt vermezse null. BIN verilirse kartın bankasına göre tablo döner.</summary>
    Task<InstallmentTable?> GetAsync(decimal price, string? bin = null, CancellationToken cancellationToken = default);
}
