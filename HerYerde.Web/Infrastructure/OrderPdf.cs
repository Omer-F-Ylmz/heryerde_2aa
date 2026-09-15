using HerYerde.Business.Dtos;
using HerYerde.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;

namespace HerYerde.Web.Infrastructure;

/// <summary>Paketleme ve kargo belgeleri: sipariş fişi (A5, kalemler/adet/stok kodu/hediye/not) ve 10x15 cm kargo etiketi (alıcı, telefon,
/// adres, sipariş numarası ve Code128 barkodu). Her sipariş bir sayfa; toplu yazdırmada sayfalar arka arkaya.</summary>
public static class OrderPdf
{
    static OrderPdf() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Slips(IReadOnlyList<OrderDetail> orders)
        => Document.Create(document =>
        {
            foreach (var (order, items, _, _) in orders)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(1.2f, Unit.Centimetre);
                    page.DefaultTextStyle(style => style.FontSize(10).LineHeight(1.3f));
                    page.Header().Column(header =>
                    {
                        header.Item().Text("Paketleme listesi").FontSize(9).FontColor(Colors.Grey.Darken2);
                        header.Item().Text(order.OrderNo).FontSize(16).SemiBold();
                        header.Item().Text($"{IstanbulTime.Format(order.CreatedAt)} · {order.FullName} · {order.District} / {order.City}");
                    });
                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Spacing(4);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(5);
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(40);
                            });
                            table.Header(head =>
                            {
                                head.Cell().PaddingBottom(4).Text("Ürün").SemiBold();
                                head.Cell().PaddingBottom(4).Text("Stok kodu").SemiBold();
                                head.Cell().PaddingBottom(4).AlignRight().Text("Adet").SemiBold();
                            });
                            foreach (var item in items)
                            {
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(4)
                                    .Text(item.IsGift ? item.ProductName + " (hediye)" : item.ProductName);
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(4).Text(item.Sku);
                                table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(4).AlignRight().Text(item.Quantity.ToString());
                            }
                        });
                        column.Item().PaddingTop(6).Text($"{items.Count} kalem · {items.Sum(i => i.Quantity)} adet").SemiBold();
                        if (order.Note is { Length: > 0 } note)
                        {
                            column.Item().PaddingTop(6).Text(text =>
                            {
                                text.Span("Not: ").SemiBold();
                                text.Span(note);
                            });
                        }
                    });
                });
            }
        }).GeneratePdf();

    public static byte[] Labels(IReadOnlyList<OrderDetail> orders)
        => Document.Create(document =>
        {
            foreach (var (order, _, _, _) in orders)
            {
                document.Page(page =>
                {
                    page.Size(10, 15, Unit.Centimetre);
                    page.Margin(0.6f, Unit.Centimetre);
                    page.DefaultTextStyle(style => style.FontSize(11).LineHeight(1.3f));
                    page.Content().Column(column =>
                    {
                        column.Spacing(6);
                        column.Item().Text("ALICI").FontSize(8).FontColor(Colors.Grey.Darken2);
                        column.Item().Text(order.FullName).FontSize(15).SemiBold();
                        column.Item().Text(order.Phone);
                        column.Item().Text(order.Address);
                        column.Item().Text($"{order.District} / {order.City}").SemiBold();
                        column.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        column.Item().Text("GÖNDEREN: HerYerde · [MÜŞTERİ: gönderici adresi ve telefon]").FontSize(8);
                        column.Item().PaddingTop(8).Image(Code128Png(order.OrderNo)).FitWidth().UseOriginalImage();
                        column.Item().AlignCenter().Text(order.OrderNo).FontSize(12).SemiBold();
                    });
                });
            }
        }).GeneratePdf();

    /// <summary>Barkod çizgileri piksel kaymasın diye ölçekleme yapılmadan, kenar boşluklu siyah-beyaz PNG.</summary>
    public static byte[] Code128Png(string text)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions { Height = 120, Width = 600, Margin = 20, PureBarcode = true }
        };
        var pixels = writer.Write(text);
        using var image = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(pixels.Pixels, pixels.Width, pixels.Height);
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }
}
