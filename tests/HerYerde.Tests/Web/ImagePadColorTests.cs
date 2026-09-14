using HerYerde.Web.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HerYerde.Tests.Web;

/// <summary>GÖZ-FIX-2: kare dolgu rengi artık sabit krem değil, kaynağın kenar piksellerinin medyanı.</summary>
public sealed class ImagePadColorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "heryerde-dolgu-" + Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Beyaz_zeminli_dikey_gorselin_dolgusu_beyaza_yakindir()
    {
        var pad = await PadOfAsync(TestImage.Framed(200, 300, new Rgba32(255, 255, 255), new Rgba32(40, 90, 160)));

        Assert.True(pad.R >= 245 && pad.G >= 245 && pad.B >= 245, $"beklenen beyaza yakın, ölçülen {pad}");
    }

    [Fact]
    public async Task Krem_zeminli_dikey_gorselin_dolgusu_kremdir()
    {
        var pad = await PadOfAsync(TestImage.Framed(200, 300, new Rgba32(0xF6, 0xF1, 0xE8), new Rgba32(40, 90, 160)));

        AssertNear(new Rgba32(0xF6, 0xF1, 0xE8), pad);
    }

    [Fact]
    public async Task Gri_zeminli_gorselin_dolgusu_krem_degil_gridir()
    {
        var pad = await PadOfAsync(TestImage.Framed(200, 300, new Rgba32(0x2B, 0x2B, 0x2E), new Rgba32(230, 230, 230)));

        AssertNear(new Rgba32(0x2B, 0x2B, 0x2E), pad);
    }

    private static void AssertNear(Rgba32 expected, Rgba32 actual)
    {
        Assert.True(
            Math.Abs(expected.R - actual.R) <= 6 && Math.Abs(expected.G - actual.G) <= 6 && Math.Abs(expected.B - actual.B) <= 6,
            $"beklenen {expected}, ölçülen {actual}");
    }

    /// <summary>Dikey görsel kareye oturunca sol kenar tamamen dolgudur; 800'lüğün orta-sol pikseli okunur.</summary>
    private async Task<Rgba32> PadOfAsync(byte[] source)
    {
        var storage = new ProductImageStorage(_root);
        await using var content = new MemoryStream(source);
        var url = await storage.SaveAsync(7, content);

        var file = Path.Combine(_root, "uploads", url.TrimStart('/')["uploads/".Length..].Replace('/', Path.DirectorySeparatorChar));
        using var written = await Image.LoadAsync<Rgba32>(file);
        return written[8, written.Height / 2];
    }
}
