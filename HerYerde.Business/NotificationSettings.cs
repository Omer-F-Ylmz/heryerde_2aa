namespace HerYerde.Business;

/// <summary>Sipariş e-postalarının SMTP ayarı. Alanlar boşken uygulama çalışır, gönderim atlanır.</summary>
public sealed class NotificationSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Gönderen adresi.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>"Yeni sipariş" bildiriminin gittiği mağaza adresi.</summary>
    public string StoreTo { get; set; } = string.Empty;
}
