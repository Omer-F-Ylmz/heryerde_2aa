using System.Globalization;
using ClosedXML.Excel;
using HerYerde.Business.Dtos;

namespace HerYerde.Web.Infrastructure;

/// <summary>Toplu ürün tablosu (.xlsx). İlk sayfa "Ürünler": başlık satırı sabit sütun adları, her satır bir ürün ya da
/// varyant. Hücreler metin olarak okunur; sayı hücresi nokta ondalıklı metne çevrilir.</summary>
public static class ProductSheet
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const int MaxRows = 5000;
    public const long MaxBytes = 8L * 1024 * 1024;
    private const string SheetName = "Ürünler";

    public static readonly (string Name, string Help)[] Columns =
    [
        ("id", "Var olan ürünü kimliğiyle eşler (dışa aktarmadan gelir). Yeni üründe boş."),
        ("ad", "Ürün adı; ürün satırında zorunlu."),
        ("slug", "Adres adı (küçük harf, rakam, tire). Boşsa addan üretilir; varyant satırında hangi ürünün varyantı olduğunu söyler."),
        ("kategori_slug", "Kategorinin slug'ı (ör. mutfak-sofra); yoksa satır hatalıdır."),
        ("aciklama", "Ürün açıklaması."),
        ("fiyat", "Nokta ondalıklı sayı (1290.50)."),
        ("kampanya_fiyat", "İsteğe bağlı; fiyattan küçük olmalı."),
        ("kampanya_etiket", "İsteğe bağlı kısa etiket."),
        ("kampanya_bitis", "İsteğe bağlı: yyyy-AA-gg ya da yyyy-AA-gg SS:dd (UTC)."),
        ("stok", "Varyantsız üründe stok; boş = takip yok. Varyantlı ve Giyim üründe boş kalır."),
        ("yayinda", "1 = yayında, 0 = taslak."),
        ("sku", "Doluysa satır varyanttır: stok kodu varsa günceller, yoksa ekler."),
        ("eksen1", "Varyantın 1. ekseni (beden, boy …)."),
        ("eksen2", "Varyantın 2. ekseni (renk …)."),
        ("varyant_stok", "Varyant satırında zorunlu stok."),
        ("gorseller", "\";\" ile ayrılmış görsel adresleri: yerel /uploads/… ya da izinli köken. Doluysa görsel listesi bununla değişir."),
        ("olcu", "İsteğe bağlı ölçü (70x70 cm).")
    ];

    public static byte[] Write(IEnumerable<ProductSheetRow> rows)
    {
        using var book = new XLWorkbook();
        var sheet = AddHeader(book);
        var index = 2;
        foreach (var row in rows)
        {
            Fill(sheet.Row(index++), row);
        }

        sheet.Columns().AdjustToContents(1, 200, 8, 60);
        return Save(book);
    }

    /// <summary>Başlık, bir ürün ve bir varyant örneği; ikinci sayfada sütun açıklamaları.</summary>
    public static byte[] Template()
    {
        using var book = new XLWorkbook();
        var sheet = AddHeader(book);
        Fill(sheet.Row(2), new ProductSheetRow(2, "", "Çelik tencere seti", "celik-tencere-seti", "mutfak-sofra", "3 parça, indüksiyon tabanlı.",
            "1290.50", "", "", "", "", "0", "", "", "", "", "", ""));
        Fill(sheet.Row(3), new ProductSheetRow(3, "", "", "celik-tencere-seti", "", "", "", "", "", "", "", "", "CTS-24", "24 cm", "", "5", "", ""));
        sheet.Columns().AdjustToContents();

        var help = book.AddWorksheet("Açıklama");
        help.Cell(1, 1).Value = "sütun";
        help.Cell(1, 2).Value = "açıklama";
        help.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < Columns.Length; i++)
        {
            help.Cell(i + 2, 1).Value = Columns[i].Name;
            help.Cell(i + 2, 2).Value = Columns[i].Help;
        }

        help.Cell(Columns.Length + 3, 1).Value = $"En çok {MaxRows} satır ve 8 MB. Hatalı tek satır varsa hiçbir değişiklik yazılmaz.";
        help.Columns().AdjustToContents(1, 100, 8, 100);
        return Save(book);
    }

    /// <summary>Dosya .xlsx değilse (zip imzası yok ya da açılmıyor), başlık uymuyorsa ya da satır sınırı aşılıyorsa hata metni.</summary>
    public static (List<ProductSheetRow>? Rows, string? Error) Read(byte[] content)
    {
        if (content.Length < 4 || content[0] != 0x50 || content[1] != 0x4B || content[2] != 0x03 || content[3] != 0x04)
        {
            return (null, "Dosya .xlsx değil.");
        }

        try
        {
            using var book = new XLWorkbook(new MemoryStream(content));
            var sheet = book.Worksheet(1);
            if (!Columns.Select((c, i) => sheet.Cell(1, i + 1).GetString().Trim() == c.Name).All(match => match))
            {
                return (null, "İlk satır şablondaki sütun adlarıyla aynı olmalı (şablonu indirip kullanın).");
            }

            var used = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (used - 1 > MaxRows)
            {
                return (null, $"Tabloda en çok {MaxRows} satır olabilir ({used - 1} satır var).");
            }

            var rows = new List<ProductSheetRow>();
            for (var number = 2; number <= used; number++)
            {
                var cells = Enumerable.Range(1, Columns.Length).Select(c => Text(sheet.Cell(number, c))).ToArray();
                if (cells.All(c => c.Length == 0))
                {
                    continue;
                }

                rows.Add(new ProductSheetRow(number, cells[0], cells[1], cells[2], cells[3], cells[4], cells[5], cells[6], cells[7],
                    cells[8], cells[9], cells[10], cells[11], cells[12], cells[13], cells[14], cells[15], cells[16]));
            }

            return (rows, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return (null, "Dosya okunamadı; Excel'de .xlsx olarak yeniden kaydedin.");
        }
    }

    private static IXLWorksheet AddHeader(XLWorkbook book)
    {
        var sheet = book.AddWorksheet(SheetName);
        for (var i = 0; i < Columns.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Columns[i].Name;
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        return sheet;
    }

    private static void Fill(IXLRow target, ProductSheetRow row)
    {
        string[] values =
        [
            row.Id, row.Name, row.Slug, row.CategorySlug, row.Description, row.Price, row.CampaignPrice, row.CampaignLabel,
            row.CampaignEndsAt, row.Stock, row.IsActive, row.Sku, row.Axis1, row.Axis2, row.VariantStock, row.Images, row.Dimensions
        ];
        for (var i = 0; i < values.Length; i++)
        {
            // Metin olarak yazılır: Excel "0542…" ya da "1290.50"yi kendi yorumuyla değiştirmesin.
            target.Cell(i + 1).SetValue(values[i]);
        }
    }

    private static string Text(IXLCell cell) => cell.DataType switch
    {
        XLDataType.Number => cell.GetDouble().ToString("0.############", CultureInfo.InvariantCulture),
        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        XLDataType.Boolean => cell.GetBoolean() ? "1" : "0",
        _ => cell.GetString().Trim()
    };

    private static byte[] Save(XLWorkbook book)
    {
        using var buffer = new MemoryStream();
        book.SaveAs(buffer);
        return buffer.ToArray();
    }
}
