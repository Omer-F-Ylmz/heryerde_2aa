using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Tek rollü panel kullanıcısı; ASP.NET Identity yok, yalnız parola özeti ve kilit sayacı.</summary>
public class AdminUser : IEntity
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }

    /// <summary>Doluysa bu ana kadar giriş kapalı (5 hatalı denemeden sonra 15 dakika).</summary>
    public DateTime? LockedUntil { get; set; }
}
