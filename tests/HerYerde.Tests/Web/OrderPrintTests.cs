using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UglyToad.PdfPig;
using ZXing;
using ZXing.Common;

namespace HerYerde.Tests.Web;

/// <summary>D13 B3: sipariş fişi (paketleme listesi) ve 10x15 cm kargo etiketi (Code128 barkodlu) PDF; seçili siparişler tek PDF.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderPrintTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Fis_pdf_her_kalemi_adet_ve_stok_koduyla_hediye_ve_notla_listeler()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceAsync(context, "fis", 3, note: "Hediye paketi olsun");
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync($"/admin/orders/{order.Id}/fis");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var text = LegalD13Tests.PdfText(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains(order.OrderNo, text);
        Assert.Contains("3 kalem", text);
        foreach (var sku in new[] { "fis-urun-1", "fis-urun-2", "fis-urun-3" })
        {
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, System.Text.RegularExpressions.Regex.Escape(sku)));
        }

        Assert.Contains("Hediye paketi olsun", text);
    }

    [Fact]
    public async Task Etiket_pdf_10x15_alici_bilgisi_ve_siparis_numarasini_okunur_code128_barkodla_tasir()
    {
        await using var context = TestDb.NewContext();
        var order = await PlaceAsync(context, "etiket", 1);
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync($"/admin/orders/{order.Id}/etiket");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var pdf = PdfDocument.Open(bytes);
        var page = Assert.Single(pdf.GetPages());
        // 10 cm = 283,46 pt; 15 cm = 425,20 pt.
        Assert.InRange(page.Width, 282, 285);
        Assert.InRange(page.Height, 424, 427);
        var text = string.Join(" ", page.GetWords().Select(w => w.Text));
        Assert.Contains("Ayşe", text);
        Assert.Contains("05424970982", text);
        Assert.Contains("Kadıköy", text);
        Assert.Equal(order.OrderNo, DecodeBarcode(page));
    }

    [Theory]
    [InlineData("etiket")]
    [InlineData("fis")]
    public async Task Toplu_yazdirmada_sayfa_sayisi_secilen_siparis_sayisina_esit(string kind)
    {
        await using var context = TestDb.NewContext();
        var first = await PlaceAsync(context, "a", 1);
        var second = await PlaceAsync(context, "b", 2);
        var third = await PlaceAsync(context, "c", 1);
        var admin = await _factory.CreateSignedInClientAsync();

        var token = await HtmlForm.AntiforgeryTokenAsync(admin, "/admin/orders");
        var response = await admin.PostAsync("/admin/orders/yazdir", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("tur", kind),
            new("ids", first.Id.ToString()),
            new("ids", second.Id.ToString()),
            new("ids", third.Id.ToString())
        ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(3, pdf.NumberOfPages);
    }

    [Fact]
    public async Task Toplu_yazdirma_secimsiz_400()
    {
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(admin, "/admin/orders", "/admin/orders/yazdir", new() { ["tur"] = "etiket" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string? DecodeBarcode(UglyToad.PdfPig.Content.Page page)
    {
        foreach (var image in page.GetImages())
        {
            if (!image.TryGetPng(out var png))
            {
                continue;
            }

            using var bitmap = Image.Load<L8>(png);
            var pixels = new byte[bitmap.Width * bitmap.Height];
            bitmap.CopyPixelDataTo(pixels);
            var source = new RGBLuminanceSource(pixels, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8);
            var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.CODE_128], TryHarder = true } };
            if (reader.Decode(source)?.Text is { } text)
            {
                return text;
            }
        }

        return null;
    }

    private static async Task<Order> PlaceAsync(HerYerdeContext context, string prefix, int lines, string? note = null)
    {
        var cartManager = TestData.NewCartManager(context);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        for (var i = 1; i <= lines; i++)
        {
            var productId = await TestData.AddHomeProductAsync(context, $"Ürün {prefix} {i}", $"{prefix}-urun-{i}", stock: 10);
            await cartManager.AddAsync(cartId, productId, variantId: null, quantity: i);
        }

        var (status, result) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", "05424970982", "ayse@example.com", "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", note, PaymentMethod.KapidaOdeme));
        Assert.Equal(HttpStatusCode.Created, status);
        return result.Data!;
    }
}
