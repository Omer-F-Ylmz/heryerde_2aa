using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Infrastructure;

namespace HerYerde.Tests.Web;

/// <summary>D12 C1/C2: ürünler .xlsx dışa aktarılır (ürün + varyant satırları), boş şablon iner; içe aktarma önce
/// önizleme (yeni/güncellenecek/hata) gösterir, onayda tek işlemde uygular, hatalı satır varsa hiçbir şey yazmaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductTransferTests : IAsyncLifetime
{
    private static readonly string[] Headers =
        ["id", "ad", "slug", "kategori_slug", "aciklama", "fiyat", "kampanya_fiyat", "kampanya_etiket", "kampanya_bitis",
         "stok", "yayinda", "sku", "eksen1", "eksen2", "varyant_stok", "gorseller", "olcu"];

    private readonly UploadFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Disa_aktarma_urun_ve_varyant_satirlarini_verir()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
            var (productId, _) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar");
            await new EfProductVariantDal(context).AddAsync(new ProductVariant { ProductId = productId, Size = "L", Color = "Kiremit", Sku = "SALVAR-L", Stock = 2 });
            await context.SaveChangesAsync();
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var response = await admin.GetAsync("/admin/products/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        using var book = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = book.Worksheet(1);
        Assert.Equal(Headers, sheet.Row(1).Cells(1, Headers.Length).Select(c => c.GetString()));
        var rows = sheet.RowsUsed().Skip(1).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal(2, rows.Count(r => r.Cell(12).GetString().Length > 0));
        await using var check = TestDb.NewContext();
        Assert.Contains(await new EfAdminAuditLogDal(check).GetListAsync(), a => a.Action == "ürün dışa aktarma");
    }

    [Fact]
    public async Task Bos_sablon_baslik_ornek_satir_ve_aciklama_sayfasiyla_acilir()
    {
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync("/admin/products/export/sablon");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var book = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        Assert.Equal(Headers, book.Worksheet(1).Row(1).Cells(1, Headers.Length).Select(c => c.GetString()));
        Assert.NotEmpty(book.Worksheet(1).Row(2).Cell(2).GetString());
        Assert.True(book.TryGetWorksheet("Açıklama", out var help));
        Assert.Contains(help.RowsUsed(), r => r.Cell(1).GetString() == "sku");
    }

    [Fact]
    public async Task Onizleme_yeni_guncellenecek_ve_hatali_satiri_ayirir()
    {
        await SeedAsync();
        var admin = await _factory.CreateSignedInClientAsync();

        var html = await (await UploadAsync(admin, Book(
            Row(slug: "celik-tencere", name: "Çelik Tencere", category: "ev", price: "499.90", stock: "5"),
            Row(slug: "cam-surahi", name: "Cam Sürahi", category: "ev", price: "120", stock: "3"),
            Row(slug: "hasir-sepet", name: "Hasır Sepet", category: "ev", price: "-4")))).Content.ReadAsStringAsync();

        Assert.Single(Regex.Matches(html, "data-import-row=\"yeni\""));
        Assert.Single(Regex.Matches(html, "data-import-row=\"guncelle\""));
        Assert.Single(Regex.Matches(html, "data-import-row=\"hata\""));
    }

    [Fact]
    public async Task Hatali_satir_varken_onay_hicbir_seyi_yazmaz()
    {
        await SeedAsync();
        var admin = await _factory.CreateSignedInClientAsync();
        var preview = await (await UploadAsync(admin, Book(
            Row(slug: "celik-tencere", name: "Çelik Tencere", category: "ev", price: "999", stock: "5"),
            Row(slug: "cam-surahi", name: "Cam Sürahi", category: "ev", price: "120"),
            Row(slug: "hasir-sepet", name: "Hasır Sepet", category: "ev", price: "abc")))).Content.ReadAsStringAsync();

        var confirm = await ConfirmAsync(admin, preview);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Equal(450m, (await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere"))!.Price);
        Assert.Null(await new EfProductDal(context).GetAsync(p => p.Slug == "cam-surahi"));
    }

    [Fact]
    public async Task Sku_ile_varyant_stogu_guncellenir_ve_iz_yazilir()
    {
        await SeedAsync();
        var admin = await _factory.CreateSignedInClientAsync();
        var preview = await (await UploadAsync(admin, Book(
            Row(slug: "salvar", sku: "SALVAR-M", axis1: "M", axis2: "Kiremit", variantStock: "12")))).Content.ReadAsStringAsync();
        Assert.Single(Regex.Matches(preview, "data-import-row=\"guncelle\""));

        var confirm = await ConfirmAsync(admin, preview);

        Assert.Equal(HttpStatusCode.Found, confirm.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Equal(12, (await new EfProductVariantDal(context).GetAsync(v => v.Sku == "SALVAR-M"))!.Stock);
        Assert.Contains(await new EfAdminAuditLogDal(context).GetListAsync(), a => a.Action == "ürün içe aktarma" && a.Detail!.Contains("1 güncellendi"));
    }

    [Fact]
    public async Task Eski_disa_aktarim_satistan_sonra_yuklenince_dokunulmamis_stok_ezilmez_degistirilen_yazilir()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
            var (productId, _) = await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 4);
            await new EfProductVariantDal(context).AddAsync(new ProductVariant { ProductId = productId, Size = "L", Color = "Kiremit", Sku = "SALVAR-L", Stock = 2 });
            await context.SaveChangesAsync();
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var exported = await (await admin.GetAsync("/admin/products/export")).Content.ReadAsByteArrayAsync();

        // Dosya Excel'de dururken satış olur.
        await using (var context = TestDb.NewContext())
        {
            (await new EfProductDal(context).GetTrackedAsync(p => p.Slug == "celik-tencere"))!.Stock = 3;
            (await new EfProductVariantDal(context).GetTrackedAsync(v => v.Sku == "SALVAR-M"))!.Stock = 1;
            await context.SaveChangesAsync();
        }

        byte[] edited;
        using (var book = new XLWorkbook(new MemoryStream(exported)))
        {
            var rows = book.Worksheet(1).RowsUsed().Skip(1).ToList();
            rows.Single(r => r.Cell(3).GetString() == "celik-tencere" && r.Cell(12).GetString().Length == 0).Cell(6).SetValue("499.90");
            rows.Single(r => r.Cell(12).GetString() == "SALVAR-L").Cell(15).SetValue("7");
            using var buffer = new MemoryStream();
            book.SaveAs(buffer);
            edited = buffer.ToArray();
        }

        var preview = await (await UploadAsync(admin, edited)).Content.ReadAsStringAsync();
        var confirm = await ConfirmAsync(admin, preview);

        Assert.Equal(HttpStatusCode.Found, confirm.StatusCode);
        await using var check = TestDb.NewContext();
        var tencere = (await new EfProductDal(check).GetAsync(p => p.Slug == "celik-tencere"))!;
        Assert.Equal(499.90m, tencere.Price);
        Assert.Equal(3, tencere.Stock);
        Assert.Equal(1, (await new EfProductVariantDal(check).GetAsync(v => v.Sku == "SALVAR-M"))!.Stock);
        Assert.Equal(7, (await new EfProductVariantDal(check).GetAsync(v => v.Sku == "SALVAR-L"))!.Stock);
    }

    [Fact]
    public async Task Kategori_slug_yoksa_satir_hatalidir()
    {
        await SeedAsync();
        var admin = await _factory.CreateSignedInClientAsync();

        var html = await (await UploadAsync(admin, Book(
            Row(slug: "cam-surahi", name: "Cam Sürahi", category: "olmayan-kategori", price: "120")))).Content.ReadAsStringAsync();

        Assert.Single(Regex.Matches(html, "data-import-row=\"hata\""));
        Assert.Contains("Kategori bulunamadı: olmayan-kategori", html);
    }

    [Fact]
    public async Task Izinsiz_kokenden_gorsel_adresi_satiri_hatali_yapar()
    {
        await SeedAsync();
        var admin = await _factory.CreateSignedInClientAsync();

        var html = await (await UploadAsync(admin, Book(
            Row(slug: "cam-surahi", name: "Cam Sürahi", category: "ev", price: "120", images: "/uploads/a.webp; https://kotu.example/b.jpg")))).Content.ReadAsStringAsync();

        Assert.Single(Regex.Matches(html, "data-import-row=\"hata\""));
        Assert.Contains("https://kotu.example/b.jpg", html);
    }

    [Fact]
    public async Task Bes_bin_bir_satir_400()
    {
        var admin = await _factory.CreateSignedInClientAsync();
        var rows = Enumerable.Range(1, 5001).Select(i => Row(slug: $"urun-{i}", name: $"Ürün {i}", category: "ev", price: "10")).ToArray();

        var response = await UploadAsync(admin, Book(rows));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("5000", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Xlsx_adli_calistirilabilir_icerik_400()
    {
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await UploadAsync(admin, [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>KAPANIŞ-3 S-03: 8 MB'a sığan ama açılınca yüzlerce MB olan xlsx (sıkıştırma bombası) kitap belleğe alınmadan reddedilir.</summary>
    [Fact]
    public async Task Acilmis_boyutu_tavani_asan_xlsx_okunmadan_400()
    {
        var bomb = new MemoryStream();
        using (var source = new MemoryStream(Book(Row(slug: "cam-surahi", name: "Cam Sürahi", category: "ev", price: "10"))))
        using (var input = new System.IO.Compression.ZipArchive(source, System.IO.Compression.ZipArchiveMode.Read))
        using (var output = new System.IO.Compression.ZipArchive(bomb, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                var copy = output.CreateEntry(entry.FullName, System.IO.Compression.CompressionLevel.SmallestSize);
                using var target = copy.Open();
                using (var original = entry.Open())
                {
                    original.CopyTo(target);
                }

                if (entry.FullName.EndsWith("sheet1.xml", StringComparison.Ordinal))
                {
                    // XML sonrasına boşluk: ayrıştırıcı için geçersiz olsa da açılmış boyut 120 MB olur.
                    var padding = new byte[1024 * 1024];
                    Array.Fill(padding, (byte)' ');
                    for (var i = 0; i < 120; i++)
                    {
                        target.Write(padding);
                    }
                }
            }
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var response = await UploadAsync(admin, bomb.ToArray());

        Assert.True(bomb.Length < ProductSheet.MaxBytes);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("açılmış boyutu", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Girissiz_disa_aktarma_girise_yonlenir()
    {
        var anonymous = _factory.CreateNonRedirectingClient();

        var response = await anonymous.GetAsync("/admin/products/export");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/admin/auth/login", response.Headers.Location!.OriginalString);
    }

    private static async Task SeedAsync()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        await TestData.AddClothingProductAsync(context, "Şalvar", "salvar", stock: 4);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient admin, byte[] bytes)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(admin, "/admin/products/import");
        using var content = new MultipartFormDataContent { { new StringContent(token), "__RequestVerificationToken" } };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "File", "urunler.xlsx");
        return await admin.PostAsync("/admin/products/import", content);
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient admin, string previewHtml)
    {
        var token = Regex.Match(previewHtml, "name=\"Token\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var antiforgery = Regex.Match(previewHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return admin.PostAsync("/admin/products/import/onayla", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Token"] = token,
            ["__RequestVerificationToken"] = antiforgery
        }));
    }

    private static string[] Row(
        string slug = "",
        string name = "",
        string category = "",
        string price = "",
        string stock = "",
        string sku = "",
        string axis1 = "",
        string axis2 = "",
        string variantStock = "",
        string images = "")
        => ["", name, slug, category, "", price, "", "", "", stock, "1", sku, axis1, axis2, variantStock, images, ""];

    private static byte[] Book(params string[][] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Ürünler");
        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = Headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        using var buffer = new MemoryStream();
        book.SaveAs(buffer);
        return buffer.ToArray();
    }
}
