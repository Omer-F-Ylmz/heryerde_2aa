using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Web.Infrastructure;

namespace HerYerde.Tests.Web;

/// <summary>D5-B5: --ithal komutu docs/ithal-1.md'yi okuyup ürün ve işlenmiş görsel üretir; ikinci koşu hiçbir şey eklemez.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ImportCommandTests : IAsyncLifetime
{
    private readonly UploadFactory _factory = new();
    private readonly string _raw = Path.Combine(Path.GetTempPath(), "heryerde-ham-" + Guid.NewGuid().ToString("n"));

    public async Task InitializeAsync()
    {
        await TestDb.ResetAsync();
        await using var context = TestDb.NewContext();
        await TestData.AddChildCategoryAsync(context, "Sepet & Dekor", "sepet-dekor");
        Directory.CreateDirectory(_raw);
        await File.WriteAllBytesAsync(Path.Combine(_raw, "sepet.jpg"), TestImage.Png(300, 200));
        await File.WriteAllBytesAsync(Path.Combine(_raw, "saksi.jpg"), TestImage.Png(200, 300, 40, 120, 90));
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

    private string WriteTable()
    {
        var path = Path.Combine(_raw, "ithal-test.md");
        File.WriteAllText(path, """
            | dosya | ad | slug | kategori | açıklama | parça | not |
            | --- | --- | --- | --- | --- | --- | --- |
            | sepet.jpg | Hasır Piknik Sepeti | hasir-piknik-sepeti | sepet-dekor | Kapaklı, taşıma saplı sepet. | 1 parça | — |
            | saksi.jpg | Hasır Saksı Sepeti | hasir-saksi-sepeti | sepet-dekor | Örgü saksı kılıfı. | 1 parça | — |
            """);
        return path;
    }

    [Fact]
    public async Task Ithal_urunleri_fiyatsiz_taslak_olarak_ve_gorselleri_uretir()
    {
        var summary = await ImportCommand.RunAsync(_factory.Services, _raw, WriteTable());

        Assert.Equal(2, summary.Products);
        Assert.Equal(2, summary.Images);
        Assert.Equal(0, summary.Skipped);

        await using var context = TestDb.NewContext();
        var products = await new EfProductDal(context).GetListAsync();
        Assert.Equal(2, products.Count);
        Assert.All(products, product =>
        {
            Assert.False(product.IsActive);
            Assert.Equal(1m, product.Price);
        });

        var images = await new EfProductImageDal(context).GetListAsync();
        Assert.Equal(2, images.Count);
        Assert.All(images, image => Assert.EndsWith("-800.webp", image.Url, StringComparison.Ordinal));

        var written = Directory.GetFiles(Path.Combine(_factory.Root, "uploads", "products"), "*.webp", SearchOption.AllDirectories);
        Assert.Equal(6, written.Length);
    }

    [Fact]
    public async Task Ikinci_kosuda_ayni_dosyalar_atlanir()
    {
        var table = WriteTable();
        await ImportCommand.RunAsync(_factory.Services, _raw, table);

        var again = await ImportCommand.RunAsync(_factory.Services, _raw, table);

        Assert.Equal(0, again.Products);
        Assert.Equal(0, again.Images);
        Assert.Equal(2, again.Skipped);

        await using var context = TestDb.NewContext();
        Assert.Equal(2, (await new EfProductDal(context).GetListAsync()).Count);
        Assert.Equal(2, (await new EfProductImageDal(context).GetListAsync()).Count);
    }
}
