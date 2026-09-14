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
}
