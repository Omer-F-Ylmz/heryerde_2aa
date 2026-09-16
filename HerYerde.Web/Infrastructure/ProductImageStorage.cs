using System.Globalization;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HerYerde.Web.Infrastructure;

public interface IProductImageStorage
{
    /// <summary>Yüklenen dosyaların kök klasörü; ithal durumu da buraya yazılır.</summary>
    string UploadsPath { get; }

    /// <summary>Görseli 1:1 kareye kendi zemin rengiyle oturtur, üç boyutta webp yazar; 800'lük adresi döner.</summary>
    Task<string> SaveAsync(int productId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Adresin üç dosyasını yeniden üretir; adres değişmez. content null ise depodaki 1200'lük kaynak alınır.
    /// Adres depo biçiminde değilse ya da kaynak yoksa false döner.</summary>
    Task<bool> RegenerateAsync(string url, Stream? content, CancellationToken cancellationToken = default);

    /// <summary>Adresin ait olduğu üç dosyayı da siler; depo dışındaki adresler sessizce atlanır.</summary>
    void Delete(string url);

    /// <summary>Kategori görseli: ortadan kırpılmış 16:9 (1200×675) ve 1:1 (800×800) webp; 16:9 adresini döner.</summary>
    Task<string> SaveCategoryAsync(int categoryId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Kategori görselinin iki kesitini siler; depo biçiminde olmayan adres atlanır.</summary>
    void DeleteCategory(string url);

    /// <summary>Marka logosu: oranı korunarak en çok 320×160 webp; adresini döner.</summary>
    Task<string> SaveBrandLogoAsync(int brandId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Depo biçimindeki marka logosunu siler; başka adres atlanır.</summary>
    void DeleteBrandLogo(string url);
}

/// <summary>Kategori görseli adresleri: kayıtta 16:9 kesit tutulur, kare kesit aynı addan türetilir.</summary>
public static class CategoryImages
{
    public const string WideSuffix = "-16x9.webp";
    public const string SquareSuffix = "-1x1.webp";

    public static string? Square(string? wideUrl)
        => wideUrl is not null && wideUrl.EndsWith(WideSuffix, StringComparison.Ordinal)
            ? wideUrl[..^WideSuffix.Length] + SquareSuffix
            : wideUrl;
}

public sealed partial class ProductImageStorage : IProductImageStorage
{
    public static readonly int[] Widths = [400, 800, 1200];

    public const int PrimaryWidth = 800;

    private readonly string _root;

    public ProductImageStorage(string root) => _root = root;

    public string UploadsPath => Path.Combine(_root, "uploads");

    public async Task<string> SaveAsync(int productId, Stream content, CancellationToken cancellationToken = default)
    {
        using var source = await Image.LoadAsync<Rgba32>(content, cancellationToken);
        var name = Guid.NewGuid().ToString("n");
        await WriteWidthsAsync(DirectoryOf(productId.ToString(CultureInfo.InvariantCulture)), name, source, cancellationToken);
        return $"/uploads/products/{productId}/{name}-{PrimaryWidth}.webp";
    }

    public async Task<bool> RegenerateAsync(string url, Stream? content, CancellationToken cancellationToken = default)
    {
        var match = UploadedUrl().Match(url);
        if (!match.Success)
        {
            return false;
        }

        var directory = DirectoryOf(match.Groups[1].Value);
        var name = match.Groups[2].Value;
        var fallback = Path.Combine(directory, $"{name}-{Widths[^1]}.webp");
        if (content is null && !File.Exists(fallback))
        {
            return false;
        }

        // Yedek kaynak yeniden yazılacak dosyanın kendisidir; önce belleğe alınır ki yazarken kilitli olmasın.
        await using var stream = content ?? new MemoryStream(await File.ReadAllBytesAsync(fallback, cancellationToken));
        using var source = await Image.LoadAsync<Rgba32>(stream, cancellationToken);
        await WriteWidthsAsync(directory, name, source, cancellationToken);
        return true;
    }

    public void Delete(string url)
    {
        var match = UploadedUrl().Match(url);
        if (!match.Success)
        {
            return;
        }

        var directory = DirectoryOf(match.Groups[1].Value);
        foreach (var width in Widths)
        {
            File.Delete(Path.Combine(directory, $"{match.Groups[2].Value}-{width}.webp"));
        }
    }

    public async Task<string> SaveCategoryAsync(int categoryId, Stream content, CancellationToken cancellationToken = default)
    {
        using var source = await Image.LoadAsync<Rgba32>(content, cancellationToken);
        var id = categoryId.ToString(CultureInfo.InvariantCulture);
        var directory = Path.Combine(UploadsPath, "categories", id);
        var name = Guid.NewGuid().ToString("n");
        Directory.CreateDirectory(directory);

        foreach (var (size, suffix) in new[] { (new Size(1200, 675), CategoryImages.WideSuffix), (new Size(800, 800), CategoryImages.SquareSuffix) })
        {
            // Kategori görseli bant/kart zemini olarak kullanılır: dolgu değil ortadan kırpma.
            using var cut = source.Clone(context => context.Resize(new ResizeOptions { Size = size, Mode = ResizeMode.Crop }));
            await cut.SaveAsync(Path.Combine(directory, name + suffix), new WebpEncoder { Quality = 82 }, cancellationToken);
        }

        return $"/uploads/categories/{id}/{name}{CategoryImages.WideSuffix}";
    }

    public void DeleteCategory(string url)
    {
        var match = CategoryUrl().Match(url);
        if (!match.Success)
        {
            return;
        }

        var directory = Path.Combine(UploadsPath, "categories", match.Groups[1].Value);
        File.Delete(Path.Combine(directory, match.Groups[2].Value + CategoryImages.WideSuffix));
        File.Delete(Path.Combine(directory, match.Groups[2].Value + CategoryImages.SquareSuffix));
    }

    public async Task<string> SaveBrandLogoAsync(int brandId, Stream content, CancellationToken cancellationToken = default)
    {
        using var source = await Image.LoadAsync<Rgba32>(content, cancellationToken);
        var id = brandId.ToString(CultureInfo.InvariantCulture);
        var directory = Path.Combine(UploadsPath, "brands", id);
        var name = Guid.NewGuid().ToString("n");
        Directory.CreateDirectory(directory);

        source.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(320, 160), Mode = ResizeMode.Max }));
        await source.SaveAsync(Path.Combine(directory, name + ".webp"), new WebpEncoder { Quality = 90 }, cancellationToken);
        return $"/uploads/brands/{id}/{name}.webp";
    }

    public void DeleteBrandLogo(string url)
    {
        var match = BrandLogoUrl().Match(url);
        if (match.Success)
        {
            File.Delete(Path.Combine(UploadsPath, "brands", match.Groups[1].Value, match.Groups[2].Value + ".webp"));
        }
    }

    /// <summary>Kare doldurma rengi: kaynağın dört kenarındaki piksellerin kanal bazlı medyanı.
    /// Medyan uç değerleri (logo, etiket, gölge köşesi) kendiliğinden kırpar; zemin rengi kalır.</summary>
    internal static Color EdgeColor(Image<Rgba32> source)
    {
        var reds = new List<byte>();
        var greens = new List<byte>();
        var blues = new List<byte>();

        void Sample(int x, int y)
        {
            var pixel = source[x, y];
            reds.Add(pixel.R);
            greens.Add(pixel.G);
            blues.Add(pixel.B);
        }

        var last = new Point(source.Width - 1, source.Height - 1);
        for (var x = 0; x < source.Width; x++)
        {
            Sample(x, 0);
            Sample(x, last.Y);
        }

        for (var y = 0; y < source.Height; y++)
        {
            Sample(0, y);
            Sample(last.X, y);
        }

        return new Color(new Rgba32(Median(reds), Median(greens), Median(blues)));
    }

    private static byte Median(List<byte> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }

    private string DirectoryOf(string productId) => Path.Combine(UploadsPath, "products", productId);

    private static async Task WriteWidthsAsync(string directory, string name, Image<Rgba32> source, CancellationToken cancellationToken)
    {
        var pad = EdgeColor(source);
        Directory.CreateDirectory(directory);

        foreach (var width in Widths)
        {
            using var square = source.Clone(context => context.Resize(new ResizeOptions
            {
                Size = new Size(width, width),
                Mode = ResizeMode.Pad,
                PadColor = pad
            }));

            await square.SaveAsync(
                Path.Combine(directory, $"{name}-{width}.webp"),
                new WebpEncoder { Quality = 82 },
                cancellationToken);
        }
    }

    /// <summary>Yalnız depo biçimindeki adres silinebilir; klasör ve dosya adı sunucunun ürettiği biçimde olmalı.</summary>
    [GeneratedRegex(@"^/uploads/products/(\d+)/([0-9a-f]{32})-\d+\.webp$")]
    private static partial Regex UploadedUrl();

    [GeneratedRegex(@"^/uploads/categories/(\d+)/([0-9a-f]{32})-16x9\.webp$")]
    private static partial Regex CategoryUrl();

    [GeneratedRegex(@"^/uploads/brands/(\d+)/([0-9a-f]{32})\.webp$")]
    private static partial Regex BrandLogoUrl();
}
