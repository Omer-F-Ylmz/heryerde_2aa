using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D15 A1: ürün videosu yönetimden yüklenir, sunucuda 720p mp4 + poster + önizlemeye dönüşür,
/// galeride ilk sırada kendiliğinden oynamadan çıkar ve VideoObject olarak işaretlenir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductVideoTests : IAsyncLifetime
{
    private readonly UploadFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Video_yuklenince_720p_mp4_poster_ve_onizleme_uretilir()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        var client = await _factory.CreateSignedInClientAsync();
        var response = await PostVideoAsync(client, productId, "tanitim.mp4", await SampleAsync());

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        await using var check = TestDb.NewContext();
        var video = await check.ProductVideos.SingleAsync(v => v.ProductId == productId);

        Assert.Matches($"^/uploads/videos/{productId}/[0-9a-f]{{32}}-720\\.mp4$", video.Url);
        Assert.Equal(video.Url.Replace("-720.mp4", "-poster.jpg", StringComparison.Ordinal), video.PosterUrl);
        Assert.Equal(video.Url.Replace("-720.mp4", "-onizleme.webm", StringComparison.Ordinal), video.PreviewUrl);
        Assert.Equal(3, video.Duration);

        // Kaynak 1920×1080: sunucu 720p'ye indirmiş olmalı, yükleyenin dosyası olduğu gibi durmamalı.
        var (codec, height) = await ProbeAsync(FileOf(video.Url));
        Assert.Equal("h264", codec);
        Assert.Equal(720, height);
        Assert.True(File.Exists(FileOf(video.PosterUrl)));
        Assert.True(File.Exists(FileOf(video.PreviewUrl)));
    }

    [Fact]
    public async Task Doksan_saniyeden_uzun_video_reddedilir()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        var client = await _factory.CreateSignedInClientAsync();
        var bytes = await File.ReadAllBytesAsync(RepoFile.PathOf("tests", "HerYerde.Tests", "Assets", "uzun-video.mp4"));
        var response = await PostVideoAsync(client, productId, "uzun.mp4", bytes);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Empty(await check.ProductVideos.ToListAsync());
        Assert.False(Directory.Exists(Path.Combine(_factory.Root, "uploads", "videos")));
    }

    [Fact]
    public async Task Video_olmayan_icerik_reddedilir()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        var client = await _factory.CreateSignedInClientAsync();
        var response = await PostVideoAsync(client, productId, "kotu.mp4", [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Empty(await check.ProductVideos.ToListAsync());
    }

    [Fact]
    public async Task Seksen_megabayttan_buyuk_video_reddedilir()
    {
        var file = new FormFile(new MemoryStream(new byte[16]), 0, VideoFile.MaxBytes + 1, "Video", "buyuk.mp4");

        var problem = await VideoFile.ProblemAsync(file, CancellationToken.None);

        Assert.Equal("Video 80 MB'tan büyük olamaz.", problem);
    }

    [Fact]
    public async Task Urun_sayfasinda_video_galeride_ilk_sirada_otomatik_oynamaz()
    {
        string poster;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            await TestData.AddImageAsync(context, productId, "/uploads/products/1/" + new string('a', 32) + "-800.webp");
            poster = (await TestData.AddVideoAsync(context, productId)).Replace("-720.mp4", "-poster.jpg", StringComparison.Ordinal);
        }

        var client = _factory.CreateNonRedirectingClient();
        var page = await (await client.GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();

        var player = page.IndexOf("<video", StringComparison.Ordinal);
        Assert.True(player > 0);
        Assert.Contains($"poster=\"{poster}\"", page);
        Assert.Contains("controls", page[player..(player + 400)]);
        // Sesli/otomatik oynatma yok: ziyaretçi başlatır.
        Assert.DoesNotContain("autoplay", page[player..(player + 400)]);
        // Görsel oynatıcının arkasına, küçük kareler şeridine düşer.
        Assert.True(page.IndexOf("gallery__thumbs", StringComparison.Ordinal) > player);
    }

    [Fact]
    public async Task Urun_sayfasi_VideoObject_json_ld_yazar()
    {
        string url;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            url = await TestData.AddVideoAsync(context, productId);
        }

        var client = _factory.CreateNonRedirectingClient();
        var page = await (await client.GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();

        Assert.Contains("\"@type\":\"VideoObject\"", page);
        Assert.Contains("\"duration\":\"PT3S\"", page);
        Assert.Contains($"\"contentUrl\":\"https://heryerde.test{url}\"", page);
    }

    [Fact]
    public async Task Kartta_video_onizlemesi_verilir()
    {
        string preview;
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            await TestData.AddImageAsync(context, productId, "/uploads/products/1/" + new string('a', 32) + "-800.webp");
            preview = (await TestData.AddVideoAsync(context, productId)).Replace("-720.mp4", "-onizleme.webm", StringComparison.Ordinal);
            await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
        }

        var client = _factory.CreateNonRedirectingClient();
        var listing = await (await client.GetAsync("/ev")).Content.ReadAsStringAsync();

        Assert.Contains($"data-onizleme=\"{preview}\"", listing);
        // Videosu olmayan kartta öznitelik hiç yazılmaz (boş değerle de).
        Assert.DoesNotContain("data-onizleme=\"\"", listing);
    }

    [Fact]
    public async Task Video_silinince_dosyalari_da_silinir()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        var client = await _factory.CreateSignedInClientAsync();
        await PostVideoAsync(client, productId, "tanitim.mp4", await SampleAsync());

        int videoId;
        string url;
        string posterUrl;
        string previewUrl;
        await using (var context = TestDb.NewContext())
        {
            var video = await context.ProductVideos.SingleAsync(v => v.ProductId == productId);
            (videoId, url, posterUrl, previewUrl) = (video.Id, video.Url, video.PosterUrl, video.PreviewUrl);
        }

        var token = await HtmlForm.AntiforgeryTokenAsync(client, $"/admin/products/edit/{productId}");
        var response = await client.PostAsync("/admin/products/deletevideo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["productId"] = productId.ToString(CultureInfo.InvariantCulture),
            ["videoId"] = videoId.ToString(CultureInfo.InvariantCulture)
        }));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Empty(await check.ProductVideos.ToListAsync());
        Assert.False(File.Exists(FileOf(url)));
        Assert.False(File.Exists(FileOf(posterUrl)));
        Assert.False(File.Exists(FileOf(previewUrl)));
    }

    [Fact]
    public async Task Video_yuklemesi_dakikada_bes_istekle_sinirli()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        var client = await _factory.CreateSignedInClientAsync();
        // Geçersiz içerik bilerek: sınır denetimden önce işler, beş deneme kovayı doldurur.
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var allowed = await PostVideoAsync(client, productId, "kotu.mp4", [0x4D, 0x5A, 0x90, 0x00]);
            Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode);
        }

        var blocked = await PostVideoAsync(client, productId, "kotu.mp4", [0x4D, 0x5A, 0x90, 0x00]);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    private static Task<byte[]> SampleAsync()
        => File.ReadAllBytesAsync(RepoFile.PathOf("tests", "HerYerde.Tests", "Assets", "ornek-video.mp4"));

    private string FileOf(string url) => Path.Combine(_factory.Root, url.TrimStart('/'));

    private static async Task<HttpResponseMessage> PostVideoAsync(HttpClient client, int productId, string fileName, byte[] bytes)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(client, $"/admin/products/edit/{productId}");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(productId.ToString(CultureInfo.InvariantCulture)), "ProductId" }
        };

        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(part, "Video", fileName);
        return await client.PostAsync("/admin/products/addvideo", content);
    }

    private static async Task<(string Codec, int Height)> ProbeAsync(string path)
    {
        using var process = Process.Start(new ProcessStartInfo("ffprobe")
        {
            ArgumentList =
            {
                "-v", "error", "-select_streams", "v:0",
                "-show_entries", "stream=codec_name,height",
                "-of", "default=nw=1:nk=1", path
            },
            RedirectStandardOutput = true
        })!;

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (lines[0], int.Parse(lines[1], CultureInfo.InvariantCulture));
    }
}
