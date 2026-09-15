using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

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
