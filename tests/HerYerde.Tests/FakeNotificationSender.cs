using HerYerde.Business.Abstract;

namespace HerYerde.Tests;

/// <summary>Testlerde gerçek SMTP kurulmaz; gönderilenler burada birikir, hata isteğe bağlı.</summary>
public sealed class FakeNotificationSender : INotificationSender
{
    public bool IsConfigured { get; set; } = true;

    public bool Fails { get; set; }

    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (Fails)
        {
            throw new InvalidOperationException("SMTP sunucusuna ulaşılamadı.");
        }

        Sent.Add((to, subject, htmlBody));
        return Task.CompletedTask;
    }
}
