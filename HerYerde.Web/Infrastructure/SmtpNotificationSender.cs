using HerYerde.Business;
using HerYerde.Business.Abstract;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HerYerde.Web.Infrastructure;

/// <summary>Kuyruktaki postayı SMTP ile yollar. Ayar boşken <see cref="IsConfigured"/> false döner;
/// uygulama yine açılır, gönderim atlanır.</summary>
public sealed class SmtpNotificationSender : INotificationSender
{
    private readonly NotificationSettings _settings;

    public SmtpNotificationSender(IOptions<NotificationSettings> settings) => _settings = settings.Value;

    public bool IsConfigured => _settings.Host.Length > 0 && _settings.From.Length > 0;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        if (_settings.User.Length > 0)
        {
            await client.AuthenticateAsync(_settings.User, _settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
