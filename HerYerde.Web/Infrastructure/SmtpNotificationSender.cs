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
    private readonly ILegalPdfArchive _documents;

    public SmtpNotificationSender(IOptions<NotificationSettings> settings, ILegalPdfArchive documents)
    {
        _settings = settings.Value;
        _documents = documents;
    }

    public bool IsConfigured => _settings.Host.Length > 0 && _settings.From.Length > 0;

    /// <summary>Gönderilecek ileti; ek dosya varsa adıyla PDF olarak eklenir.</summary>
    public static MimeMessage BuildMessage(string from, string to, string subject, string htmlBody, string? attachmentPath, string? attachmentName)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        var body = new BodyBuilder { HtmlBody = htmlBody };
        if (attachmentPath is not null)
        {
            body.Attachments.Add(attachmentName ?? Path.GetFileName(attachmentPath), File.ReadAllBytes(attachmentPath), new ContentType("application", "pdf"));
        }

        message.Body = body.ToMessageBody();
        return message;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, string? attachment = null, CancellationToken cancellationToken = default)
    {
        // Belge üretilemiyorsa (arşivlenmemiş eski sürüm) posta eksiz gider; gövdedeki sipariş bağlantısı kalır.
        var file = attachment is null ? null : await _documents.ResolveAsync(attachment, cancellationToken);
        var message = BuildMessage(_settings.From, to, subject, htmlBody, file, file is null ? null : "on-bilgilendirme-ve-sozlesme.pdf");

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
