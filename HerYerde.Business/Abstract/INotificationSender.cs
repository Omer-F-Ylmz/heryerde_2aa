namespace HerYerde.Business.Abstract;

/// <summary>E-postayı gerçekten gönderen katman. Gönderim hatası istisnayla bildirilir; kuyruk denemeyi sayar.</summary>
public interface INotificationSender
{
    /// <summary>SMTP ayarı tamamsa true; değilse gönderim atlanır ve kayıt kuyrukta bekler.</summary>
    bool IsConfigured { get; }

    /// <summary><paramref name="attachment"/>: eklenecek belgenin gizli depodaki göreli yolu; üretilemiyorsa posta eksiz gider.</summary>
    Task SendAsync(string to, string subject, string htmlBody, string? attachment = null, CancellationToken cancellationToken = default);
}
