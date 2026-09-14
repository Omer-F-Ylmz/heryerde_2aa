using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HerYerde.Tests.Web;

/// <summary>D5-A: yönetici ürün formundan dosya yükleme; uzantı değil içerik karar verir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ImageUploadTests : IAsyncLifetime
{
    private readonly UploadFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static async Task<int> NewProductAsync()
    {
        await using var context = TestDb.NewContext();
        return await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
    }

    private string ProductDirectory(int productId)
        => Path.Combine(_factory.Root, "uploads", "products", productId.ToString());

    [Fact]
    public async Task Uzantisi_jpg_ama_icerigi_calistirilabilir_olan_dosya_reddedilir()
    {
        var productId = await NewProductAsync();
        var client = await _factory.CreateSignedInClientAsync();

        // "MZ" ile başlayan PE başlığı: uzantı jpg olsa da içerik görsel değil.
        var response = await UploadForm.PostAsync(client, productId, ("salvar.jpg", [0x4D, 0x5A, 0x90, 0x00, 0x03]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Empty(await new EfProductImageDal(context).GetListAsync());
        Assert.False(Directory.Exists(ProductDirectory(productId)));
    }

    [Fact]
    public async Task Sekiz_megabayti_asan_dosya_reddedilir()
    {
        var productId = await NewProductAsync();
        var client = await _factory.CreateSignedInClientAsync();

        var big = new byte[9 * 1024 * 1024];
        big[0] = 0xFF;
        big[1] = 0xD8;
        big[2] = 0xFF;

        var response = await UploadForm.PostAsync(client, productId, ("buyuk.jpg", big));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Empty(await new EfProductImageDal(context).GetListAsync());
    }

    [Fact]
    public async Task Png_yuklenince_uc_boyutta_webp_uretilir_ve_url_sekiz_yuzu_gosterir()
    {
        var productId = await NewProductAsync();
        var client = await _factory.CreateSignedInClientAsync();

        var response = await UploadForm.PostAsync(client, productId, ("salvar.png", TestImage.Png(300, 300)));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        await using var context = TestDb.NewContext();
        var image = Assert.Single(await new EfProductImageDal(context).GetListAsync());
        Assert.EndsWith("-800.webp", image.Url, StringComparison.Ordinal);
        Assert.StartsWith($"/uploads/products/{productId}/", image.Url, StringComparison.Ordinal);

        var files = Directory.GetFiles(ProductDirectory(productId)).Select(Path.GetFileName).ToList();
        Assert.Equal(3, files.Count);
        foreach (var width in new[] { 400, 800, 1200 })
        {
            Assert.Contains(files, name => name!.EndsWith($"-{width}.webp", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Kare_doldurmada_oran_korunur_kenarlar_kaynagin_zemin_rengiyle_dolar()
    {
        var productId = await NewProductAsync();
        var client = await _factory.CreateSignedInClientAsync();

        // 400x200: krem zeminli, kırmızı göbekli. Kare içinde tam yarım yükseklik kaplamalı, üst ve alt zeminden dolmalı.
        await UploadForm.PostAsync(client, productId, ("genis.png", TestImage.Framed(400, 200, new Rgba32(0xF6, 0xF1, 0xE8), new Rgba32(220, 30, 30))));

        var path = Directory.GetFiles(ProductDirectory(productId), "*-800.webp").Single();
        using var square = Image.Load<Rgba32>(path);

        Assert.Equal(800, square.Width);
        Assert.Equal(800, square.Height);
        Assert.True(IsCream(square[400, 4]), "Üst kenar zemin rengiyle dolmalı.");
        Assert.True(IsCream(square[400, 795]), "Alt kenar zemin rengiyle dolmalı.");
        Assert.True(IsRed(square[400, 400]), "Görselin kendisi ortada durmalı.");
        Assert.True(IsCream(square[4, 400]), "Görsel kare genişliğini tam kaplamalı; kenarı krem zemin.");

        var imageRows = Enumerable.Range(0, 800).Count(y => IsRed(square[400, y]));
        Assert.InRange(imageRows, 220, 260);

        static bool IsCream(Rgba32 pixel) => Math.Abs(pixel.R - 0xF6) < 12 && Math.Abs(pixel.G - 0xF1) < 12 && Math.Abs(pixel.B - 0xE8) < 12;
        static bool IsRed(Rgba32 pixel) => pixel.R > 150 && pixel.G < 90 && pixel.B < 90;
    }

    [Fact]
    public async Task Dosya_adiyla_yol_kacisi_denemesi_reddedilir()
    {
        var productId = await NewProductAsync();
        var client = await _factory.CreateSignedInClientAsync();

        var response = await UploadForm.PostAsync(client, productId, ("../../../wwwroot/css/site.css", TestImage.Png(50, 50)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Empty(await new EfProductImageDal(context).GetListAsync());
    }

    [Fact]
    public async Task Gorsel_silinince_uc_dosya_da_silinir()
    {
        var productId = await NewProductAsync();
        var client = await _factory.CreateSignedInClientAsync();
        await UploadForm.PostAsync(client, productId, ("salvar.png", TestImage.Png(200, 200)));

        int imageId;
        await using (var context = TestDb.NewContext())
        {
            imageId = (await new EfProductImageDal(context).GetListAsync()).Single().Id;
        }

        Assert.Equal(3, Directory.GetFiles(ProductDirectory(productId)).Length);

        var response = await HtmlForm.PostAsync(client, $"/admin/products/edit/{productId}", "/admin/products/deleteimage", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["imageId"] = imageId.ToString()
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Empty(Directory.GetFiles(ProductDirectory(productId)));
    }

    [Fact]
    public async Task Anonim_yukleme_girise_yonlenir()
    {
        var productId = await NewProductAsync();
        var client = _factory.CreateNonRedirectingClient();

        var response = await client.PostAsync("/admin/products/addimage", new MultipartFormDataContent
        {
            { new StringContent(productId.ToString()), "ProductId" }
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/admin/auth/login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Yirmi_birinci_yukleme_429_alir()
    {
        var productId = await NewProductAsync();
        var client = await _factory.CreateSignedInClientAsync();
        var png = TestImage.Png(24, 24);

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            var allowed = await UploadForm.PostAsync(client, productId, ($"kare-{attempt}.png", png));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var blocked = await UploadForm.PostAsync(client, productId, ("kare-21.png", png));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }
}
