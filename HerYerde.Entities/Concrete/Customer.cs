using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>İsteğe bağlı üye hesabı; misafir alışveriş sürer. ASP.NET Identity yok: parola özeti (boşsa parolasız hesap),
/// tek kullanımlık bağlantıların SHA-256 özetleri ve oturum damgası.</summary>
public class Customer : IEntity
{
    public int Id { get; set; }

    /// <summary>Küçük harf; tabloda tekil.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Boşsa hesap yalnız e-postaya gelen giriş bağlantısıyla açılır.</summary>
    public string? PasswordHash { get; set; }

    public string? FullName { get; set; }

    /// <summary>Normalleştirilmiş cep telefonu ("05XXXXXXXXX"); doğrulanmaz, sipariş eşlemesinde kullanılmaz.</summary>
    public string? Phone { get; set; }

    /// <summary>Doğrulama, giriş bağlantısı ya da parola sıfırlamayla e-postanın sahibine ait olduğu kanıtlandığı an.
    /// Boşken parolayla giriş açılmaz ve misafir siparişleri bağlanmaz.</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>Kayıtta aydınlatma metninin okunduğunun onay anı ve metnin sürümü (zorunlu).</summary>
    public DateTime KvkkConsentAt { get; set; }

    public string LegalVersion { get; set; } = string.Empty;

    /// <summary>Kampanya iletisi izni; varsayılan kapalı. Gönderim yok (İYS kaydı yapılmaz), yalnız kayıt.</summary>
    public bool MarketingConsent { get; set; }

    /// <summary>İznin son verildiği ya da geri alındığı an.</summary>
    public DateTime? MarketingConsentAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Parola değişince ya da "tüm cihazlardan çık" denince ilerler; çerezdeki damga uymazsa oturum düşer.</summary>
    public DateTime SessionStamp { get; set; }

    public string? VerifyTokenHash { get; set; }
    public DateTime? VerifyTokenExpiresAt { get; set; }

    /// <summary>Parolasız giriş bağlantısı: 15 dakika, tek kullanımlık.</summary>
    public string? LoginTokenHash { get; set; }
    public DateTime? LoginTokenExpiresAt { get; set; }

    public string? ResetTokenHash { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
}
