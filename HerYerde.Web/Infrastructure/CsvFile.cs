using System.Globalization;
using System.Text;

namespace HerYerde.Web.Infrastructure;

/// <summary>Excel'in Türkçe ayarında çift tıkla doğru açılan CSV: UTF-8 BOM, ";" ayraç, virgüllü ondalık, CRLF satır sonu.</summary>
public static class CsvFile
{
    public const string ContentType = "text/csv";

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static byte[] Build(IEnumerable<IReadOnlyList<string>> rows)
    {
        var text = new StringBuilder();
        foreach (var row in rows)
        {
            text.AppendJoin(';', row.Select(Field)).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    /// <summary>"979,90": binlik ayraç yok, Excel sayı olarak okur.</summary>
    public static string Money(decimal amount) => amount.ToString("0.00", Turkish);

    /// <summary>Ayraç, tırnak ya da satır sonu içeren alan tırnaklanır. "=", "+", "-", "@" ile başlayan metin formül olarak
    /// çalışmasın diye başına kesme işareti alır (CSV enjeksiyonu).</summary>
    private static string Field(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
