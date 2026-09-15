using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Tek rollü panel kullanıcısı; ASP.NET Identity yok, yalnız parola özeti ve kilit sayacı.</summary>
public class AdminUser : IEntity
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Hatalı parola ve hatalı iki adımlı kod birlikte sayılır; 5'te kilit.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>Doluysa bu ana kadar giriş kapalı (5 hatalı denemeden sonra 15 dakika).</summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>Parola değiştiğinde ya da "tüm oturumları kapat" denince ilerler; çerezdeki damga buna uymazsa oturum düşer.</summary>
    public DateTime PasswordChangedAt { get; set; }

    /// <summary>CLI ile geçici parola verildi: ilk girişte parola değiştirilmeden panel açılmaz.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Sıfırlama bağlantısındaki anahtarın SHA-256 özeti; kullanılınca silinir.</summary>
    public string? ResetTokenHash { get; set; }

    public DateTime? ResetTokenExpiresAt { get; set; }

    /// <summary>TOTP gizli anahtarı (Base32). Kurulum sayfası açılınca yazılır, kod doğrulanınca etkinleşir.</summary>
    public string? TotpSecret { get; set; }

    public bool TotpEnabled { get; set; }

    /// <summary>Girişte kabul edilen son TOTP adımı (Unix sn / 30); aynı ya da daha eski adımın kodu yeniden kabul edilmez.</summary>
    public long? TotpLastStep { get; set; }

    /// <summary>Kullanılmamış yedek kodların SHA-256 özetleri, ";" ile ayrılmış; kullanılan kod listeden düşer.</summary>
    public string? RecoveryCodeHashes { get; set; }
}
