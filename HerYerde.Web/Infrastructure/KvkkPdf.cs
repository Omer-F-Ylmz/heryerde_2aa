using System.Collections;
using System.Globalization;
using HerYerde.Business.Dtos;
using HerYerde.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HerYerde.Web.Infrastructure;

/// <summary>KVKK erişim dökümünün okunur PDF'i: her tablo bir bölüm, her kayıt alan adı ve değeriyle. JSON dökümüyle aynı veri.</summary>
public static class KvkkPdf
{
    static KvkkPdf() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly (string Title, Func<KvkkPerson, IList> Rows)[] Sections =
    [
        ("Siparişler", p => p.Orders),
        ("Sipariş kalemleri", p => p.OrderItems),
        ("Kart ödemeleri", p => p.Payments),
        ("Havale bildirimleri", p => p.PaymentNotices),
        ("İade ve değişim talepleri", p => p.ReturnRequests),
        ("İade talebi kalemleri", p => p.ReturnRequestItems),
        ("İletişim mesajları", p => p.ContactMessages),
        ("Ürün yorumları", p => p.Reviews)
    ];

    public static byte[] Dump(KvkkPerson person, DateTime nowUtc)
        => Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10).LineHeight(1.4f));
            page.Header().PaddingBottom(12).Text($"HerYerde · Kişisel veri erişim dökümü · {IstanbulTime.Format(nowUtc)}").FontSize(9).FontColor(Colors.Grey.Darken2);
            page.Content().Column(column =>
            {
                column.Spacing(6);
                column.Item().Text("Kişisel veri erişim dökümü").FontSize(18).SemiBold();
                column.Item().Text("KVKK m. 11 başvurusu üzerine, başvurana ait kayıtlar. Kişi: " + person.Subject);
                foreach (var (title, rows) in Sections)
                {
                    var list = rows(person);
                    column.Item().PaddingTop(10).Text($"{title} ({list.Count})").FontSize(13).SemiBold();
                    foreach (var row in list)
                    {
                        column.Item().Text(Describe(row!));
                    }
                }
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(9));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();

    /// <summary>"Alan: değer · Alan: değer"; boş alanlar yazılmaz, anlar İstanbul saatiyle.</summary>
    private static string Describe(object row)
        => string.Join(" · ", row.GetType().GetProperties()
            .Select(p => (p.Name, Value: p.GetValue(row)))
            .Where(p => p.Value is not null && p.Value.ToString() is { Length: > 0 })
            .Select(p => $"{p.Name}: {p.Value switch
            {
                DateTime at => IstanbulTime.Format(at),
                decimal amount => amount.ToString("0.00", CultureInfo.InvariantCulture),
                _ => p.Value!.ToString()
            }}"));
}
