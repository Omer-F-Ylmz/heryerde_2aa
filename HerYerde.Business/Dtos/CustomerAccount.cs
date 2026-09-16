using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Dtos;

/// <summary>Password boşsa parolasız hesap (giriş e-postaya gelen bağlantıyla).</summary>
public sealed record CustomerSignUp(string Email, string? Password, bool KvkkConsent, bool MarketingConsent);

public sealed record CustomerProfile(string? FullName, string? Phone, bool MarketingConsent);

/// <summary>Hesap silme sonucu: anonimleştirilen siparişler (denetim izi için) ve silinecek gizli dosyalar.</summary>
public sealed record CustomerDeletion(IReadOnlyList<Order> AnonymizedOrders, IReadOnlyList<string> Files);

/// <summary>KVKK erişim dökümündeki üye hesabı: parola özeti ve bağlantı anahtarları dökümde yer almaz.</summary>
public sealed record KvkkAccount(
    string Email,
    string? FullName,
    string? Phone,
    DateTime CreatedAt,
    DateTime? EmailVerifiedAt,
    DateTime KvkkConsentAt,
    string LegalVersion,
    bool MarketingConsent,
    DateTime? MarketingConsentAt,
    bool HasPassword,
    IReadOnlyList<int> FavoriteProductIds);
