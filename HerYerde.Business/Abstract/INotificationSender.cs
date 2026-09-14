namespace HerYerde.Business.Abstract;

/// <summary>E-postayı gerçekten gönderen katman. Gönderim hatası istisnayla bildirilir; kuyruk denemeyi sayar.</summary>
public interface INotificationSender
{
    /// <summary>SMTP ayarı tamamsa true; değilse gönderim atlanır ve kayıt kuyrukta bekler.</summary>
    bool IsConfigured { get; }

    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
