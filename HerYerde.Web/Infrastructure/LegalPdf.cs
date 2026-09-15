using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HerYerde.Web.Infrastructure;

/// <summary>Yasal belgelerin PDF'i: ön bilgilendirme + mesafeli satış sözleşmesi (sürüm başına) ve cayma formu. QuestPDF Community
/// lisansıyla (docs/lisans-notlari.md); gövde 12 punto.</summary>
public static partial class LegalPdf
{
    public const float BodySize = 12;

    static LegalPdf() => QuestPDF.Settings.License = LicenseType.Community;

    /// <summary>Birden çok metin tek belgede; her metin başlığıyla yeni sayfada başlar, her sayfada sürüm ve tarih yazar.</summary>
    public static byte[] Contract(IReadOnlyList<(string Title, string Html)> parts, string version, DateTime updatedAt)
        => Document.Create(document =>
        {
            foreach (var (title, html) in parts)
            {
                document.Page(page =>
                {
                    Frame(page, $"Sürüm {version} · Son güncelleme {updatedAt.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR"))}");
                    page.Content().Column(column =>
                    {
                        column.Spacing(6);
                        column.Item().PaddingBottom(8).Text(title).FontSize(20).SemiBold();
                        foreach (var (heading, text) in Blocks(html))
                        {
                            var line = column.Item().PaddingTop(heading ? 10 : 0).Text(text);
                            if (heading)
                            {
                                line.FontSize(15).SemiBold();
                            }
                        }
                    });
                });
            }
        }).GeneratePdf();

    /// <summary>Mesafeli Sözleşmeler Yönetmeliği'ndeki örnek cayma formu; veri yoksa alanlar elle doldurulmak üzere boş çizgidir.</summary>
    public static byte[] WithdrawalForm(WithdrawalFormModel? data)
        => Document.Create(document => document.Page(page =>
        {
            Frame(page, "HerYerde · Cayma formu");
            page.Content().Column(column =>
            {
                column.Spacing(10);
                column.Item().Text("CAYMA FORMU").FontSize(20).SemiBold();
                column.Item().Text("(Bu form, yalnız sözleşmeden cayma hakkı kullanılmak istendiğinde doldurulup gönderilir.)").Italic();
                column.Item().Text("Kime: [MÜŞTERİ: ticari unvan], [MÜŞTERİ: açık adres], [MÜŞTERİ: e-posta adresi]");
                column.Item().Text("Bu formla aşağıdaki malların satışına ilişkin sözleşmeden cayma hakkımı kullandığımı beyan ederim.");
                foreach (var (label, value) in new[]
                         {
                             ("Sipariş numarası", data?.OrderNo),
                             ("Sipariş tarihi / teslim tarihi", data?.OrderDate),
                             ("Cayma hakkına konu ürün(ler)", data?.Items),
                             ("Ürün(lerin) bedeli", data?.Amount),
                             ("Tüketicinin adı soyadı", data?.FullName),
                             ("Tüketicinin adresi", data?.Address),
                             ("Tüketicinin imzası (yalnız kâğıt üzerinde gönderiliyorsa)", null),
                             ("Tarih", null)
                         })
                {
                    column.Item().Text(text =>
                    {
                        text.Span(label + ": ").SemiBold();
                        text.Span(string.IsNullOrWhiteSpace(value) ? new string('_', 48) : value);
                    });
                }
            });
        })).GeneratePdf();

    /// <summary>Razor çıktısından okunur bloklar: h2/h3 başlık; p, li, dt, dd paragraf (li madde imiyle). Etiketler atılır, varlıklar çözülür.</summary>
    public static IEnumerable<(bool Heading, string Text)> Blocks(string html)
    {
        foreach (Match match in BlockPattern().Matches(html))
        {
            var tag = match.Groups["tag"].Value.ToLowerInvariant();
            var text = Whitespace().Replace(WebUtility.HtmlDecode(Tags().Replace(match.Groups["inner"].Value, " ")), " ").Trim();
            if (text.Length > 0)
            {
                yield return (tag is "h2" or "h3", tag == "li" ? "• " + text : text);
            }
        }
    }

    private static void Frame(PageDescriptor page, string header)
    {
        page.Size(PageSizes.A4);
        page.Margin(2, Unit.Centimetre);
        page.DefaultTextStyle(style => style.FontSize(BodySize).LineHeight(1.4f));
        page.Header().PaddingBottom(12).Text(header).FontSize(9).FontColor(Colors.Grey.Darken2);
        page.Footer().AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(9));
            text.CurrentPageNumber();
            text.Span(" / ");
            text.TotalPages();
        });
    }

    [GeneratedRegex(@"<(?<tag>h2|h3|p|li|dt|dd)\b[^>]*>(?<inner>.*?)</\k<tag>>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

/// <summary>Sözleşme PDF arşivi: sürümün belgesi gizli depoda <see cref="LegalDocs.ArchivePath"/> altında durur.</summary>
public interface ILegalPdfArchive
{
    /// <summary>Göreli yoldaki belge varsa tam yolu; yoksa ve yol yürürlükteki sürümün sözleşmesiyse üretip arşivler; aksi halde null
    /// (arşivlenmemiş eski sürüm yeniden üretilmez: metin değişmiş olabilir).</summary>
    Task<string?> ResolveAsync(string? relativePath, CancellationToken cancellationToken = default);
}

public sealed class LegalPdfArchive(IPrivateFileStorage files, IServiceScopeFactory scopes) : ILegalPdfArchive
{
    /// <summary>PDF'e giren metinler: ödeme adımında onaylanan iki belge.</summary>
    public static readonly string[] Slugs = ["on-bilgilendirme-formu", "mesafeli-satis-sozlesmesi"];

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<string?> ResolveAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (files.Resolve(relativePath) is { } existing)
        {
            return existing;
        }

        var current = LegalDocs.ArchivePath(LegalDocs.Version);
        if (relativePath != current)
        {
            return null;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (files.Resolve(current) is { } made)
            {
                return made;
            }

            await using var scope = scopes.CreateAsyncScope();
            var parts = new List<(string Title, string Html)>();
            foreach (var slug in Slugs)
            {
                var page = LegalPages.Find(slug)!;
                parts.Add((page.Title, await RazorViewText.RenderAsync(scope.ServiceProvider, $"/Views/Legal/{slug}.cshtml", page)));
            }

            await files.WriteAsync(current, LegalPdf.Contract(parts, LegalDocs.Version, LegalDocs.UpdatedAt), cancellationToken);
            return files.Resolve(current);
        }
        finally
        {
            Gate.Release();
        }
    }
}

/// <summary>İstek dışında (arka plan postası) bir görünümü düzen sayfası olmadan metne çevirir; görünüm ViewData["Pdf"] ile düzeni kapatır.</summary>
public static class RazorViewText
{
    public const string PdfFlag = "Pdf";

    public static async Task<string> RenderAsync(IServiceProvider services, string viewPath, object model)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var routeData = new RouteData();
        routeData.Values["controller"] = "Legal";
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var view = services.GetRequiredService<IRazorViewEngine>().GetView(null, viewPath, isMainPage: false).EnsureSuccessful(null).View!;

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary())
        {
            Model = model,
            [PdfFlag] = true
        };
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        await view.RenderAsync(new ViewContext(actionContext, view, viewData, tempData, writer, new HtmlHelperOptions()));
        return writer.ToString();
    }
}
