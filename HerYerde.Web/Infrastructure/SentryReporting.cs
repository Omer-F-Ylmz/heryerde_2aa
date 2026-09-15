using Sentry;
using Sentry.Extensibility;
using Sentry.Serilog;
using Serilog.Events;

namespace HerYerde.Web.Infrastructure;

/// <summary>Hata izleme: Serilog'daki Error ve üstü olaylar Sentry'ye gider. Sentry:Dsn boşsa sink hiç eklenmez, SDK açılmaz.
/// Kişisel veri gönderilmez (SendDefaultPii kapalı); olay çıkmadan telefon/e-posta/adres PiiMaskEnricher kurallarıyla maskelenir.</summary>
public static class SentryReporting
{
    public static bool Enabled(string? dsn) => !string.IsNullOrWhiteSpace(dsn);

    /// <param name="transport">Yalnız testler DI'a kayıt taşıyıcısı koyar; üretimde null, SDK kendi HTTP taşıyıcısını kullanır.</param>
    public static void Configure(SentrySerilogOptions options, string dsn, ITransport? transport)
    {
        options.Dsn = dsn;
        options.SendDefaultPii = false;
        options.MinimumEventLevel = LogEventLevel.Error;
        options.SetBeforeSend((sentryEvent, _) => Scrub(sentryEvent));
        if (transport is not null)
        {
            options.Transport = transport;
        }
    }

    public static SentryEvent Scrub(SentryEvent sentryEvent)
    {
        if (sentryEvent.Message is { } message)
        {
            sentryEvent.Message = new SentryMessage
            {
                Message = Mask(message.Message),
                Formatted = Mask(message.Formatted),
                Params = message.Params
            };
        }

        foreach (var exception in sentryEvent.SentryExceptions ?? [])
        {
            exception.Value = Mask(exception.Value);
        }

        foreach (var (key, value) in sentryEvent.Extra.ToList())
        {
            if (PiiMaskEnricher.IsPiiName(key))
            {
                sentryEvent.SetExtra(key, PiiMaskEnricher.Mask);
            }
            else if (value is string text)
            {
                sentryEvent.SetExtra(key, PiiMaskEnricher.MaskText(text));
            }
        }

        return sentryEvent;
    }

    private static string? Mask(string? text) => text is null ? null : PiiMaskEnricher.MaskText(text);
}
