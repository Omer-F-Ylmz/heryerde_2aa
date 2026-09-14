using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Web.Infrastructure;
using SixLabors.ImageSharp.PixelFormats;

namespace HerYerde.Tests.Web;

/// <summary>GÖZ-FIX-2: --gorsel-yenile yüklenmiş görselleri adres değiştirmeden yeniden üretir; ikinci koşu aynı baytı yazar.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class RefreshImagesCommandTests : IAsyncLifetime
{
    private readonly UploadFactory _factory = new();
    private readonly string _raw = Path.Combine(Path.GetTempPath(), "heryerde-yenile-" + Guid.NewGuid().ToString("n"));

    public async Task InitializeAsync()
    {
        await TestDb.ResetAsync();
        await using var context = TestDb.NewContext();
        await TestData.AddChildCategoryAsync(context, "Sepet & Dekor", "sepet-dekor");
        Directory.CreateDirectory(_raw);
        await File.WriteAllBytesAsync(
            Path.Combine(_raw, "sepet.jpg"),
            TestImage.Framed(300, 200, new Rgba32(0xF6, 0xF1, 0xE8), new Rgba32(120, 90, 40)));
        await File.WriteAllTextAsync(Path.Combine(_raw, "ithal-test.md"), """
            | dosya | ad | slug | kategori | açıklama | parça | not |
            | --- | --- | --- | --- | --- | --- | --- |
            | sepet.jpg | Hasır piknik sepeti | hasir-piknik-sepeti | sepet-dekor | Kapaklı, taşıma saplı sepet. | 1 parça | — |
            """);
        await ImportCommand.RunAsync(_factory.Services, _raw, Path.Combine(_raw, "ithal-test.md"));
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        if (Directory.Exists(_raw))
        {
            Directory.Delete(_raw, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Ham_kaynak_varken_yeniden_uretir_ve_ikinci_kosu_ayni_bayti_yazar()
    {
        var before = await UrlsAsync();

        var first = await RefreshImagesCommand.RunAsync(_factory.Services, _raw);
        var afterFirst = Snapshot(before);

        var second = await RefreshImagesCommand.RunAsync(_factory.Services, _raw);
        var afterSecond = Snapshot(before);

        Assert.Equal(1, first.Regenerated);
        Assert.Equal(1, first.FromSource);
        Assert.Equal(first, second);
        Assert.Equal(before, await UrlsAsync());
        Assert.Equal(afterFirst, afterSecond);
        Assert.Equal(3, Directory.GetFiles(Path.Combine(_factory.Root, "uploads", "products"), "*.webp", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task Ham_kaynak_yokken_1200luk_dosyadan_uretir_ve_idempotenttir()
    {
        var before = await UrlsAsync();

        var first = await RefreshImagesCommand.RunAsync(_factory.Services, rawDirectory: null);
        var afterFirst = Snapshot(before);

        var second = await RefreshImagesCommand.RunAsync(_factory.Services, rawDirectory: null);

        Assert.Equal(1, first.Regenerated);
        Assert.Equal(0, first.FromSource);
        Assert.Equal(first, second);
        Assert.Equal(before, await UrlsAsync());
        Assert.Equal(afterFirst, Snapshot(before));
    }

    private async Task<List<string>> UrlsAsync()
    {
        await using var context = TestDb.NewContext();
        return (await new EfProductImageDal(context).GetListAsync()).Select(i => i.Url).OrderBy(u => u, StringComparer.Ordinal).ToList();
    }

    /// <summary>Adreslerin işaret ettiği 800'lük dosyaların baytları; yeniden üretim kararlıysa iki koşuda aynı kalır.</summary>
    private List<string> Snapshot(IEnumerable<string> urls)
        => urls
            .Select(url => Path.Combine(_factory.Root, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)))
            .Select(path => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))))
            .ToList();
}
