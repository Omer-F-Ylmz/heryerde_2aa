using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace HerYerde.Web.Infrastructure;

/// <summary>Telefon, e-posta ve adres loga düşmesin: alan adı bunlardan birini işaret ediyorsa değer tümüyle,
/// diğer metin alanlarında e-posta/telefon desenine uyan kısım "***" olur.</summary>
public sealed partial class PiiMaskEnricher : ILogEventEnricher
{
    private const string Mask = "***";

    private static readonly string[] PiiNames = ["phone", "telefon", "mail", "eposta", "address", "adres"];

    [GeneratedRegex(@"[^\s@""]+@[^\s@""]+\.[^\s@""]+|(?:\+?90[\s-]?)?0?5\d{2}[\s-]?\d{3}[\s-]?\d{2}[\s-]?\d{2}")]
    private static partial Regex PiiPattern();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            if (PiiNames.Any(pii => name.Contains(pii, StringComparison.OrdinalIgnoreCase)))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Mask)));
            }
            else if (value is ScalarValue { Value: string text } && PiiPattern().IsMatch(text))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(PiiPattern().Replace(text, Mask))));
            }
        }
    }
}
