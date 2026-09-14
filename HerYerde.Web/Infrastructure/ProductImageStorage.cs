using System.Globalization;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace HerYerde.Web.Infrastructure;

public interface IProductImageStorage
{
    /// <summary>Yüklenen dosyaların kök klasörü; ithal durumu da buraya yazılır.</summary>
    string UploadsPath { get; }

    /// <summary>Görseli 1:1 kareye krem dolguyla oturtur, üç boyutta webp yazar; 800'lük adresi döner.</summary>
    Task<string> SaveAsync(int productId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Adresin ait olduğu üç dosyayı da siler; depo dışındaki adresler sessizce atlanır.</summary>
    void Delete(string url);
}

public sealed partial class ProductImageStorage : IProductImageStorage
{
    public static readonly int[] Widths = [400, 800, 1200];

    public const int PrimaryWidth = 800;

    /// <summary>Kare doldurma rengi: marka kremi (brand.md "Nötr").</summary>
    private static readonly Color Pad = Color.ParseHex("F6F1E8");

    private readonly string _root;

    public ProductImageStorage(string root) => _root = root;

    public string UploadsPath => Path.Combine(_root, "uploads");

    public async Task<string> SaveAsync(int productId, Stream content, CancellationToken cancellationToken = default)
    {
        using var source = await Image.LoadAsync(content, cancellationToken);
        var name = Guid.NewGuid().ToString("n");
        var directory = Path.Combine(UploadsPath, "products", productId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);

        foreach (var width in Widths)
        {
            using var square = source.Clone(context => context.Resize(new ResizeOptions
            {
                Size = new Size(width, width),
                Mode = ResizeMode.Pad,
                PadColor = Pad
            }));

            await square.SaveAsync(
                Path.Combine(directory, $"{name}-{width}.webp"),
                new WebpEncoder { Quality = 82 },
                cancellationToken);
        }

        return $"/uploads/products/{productId}/{name}-{PrimaryWidth}.webp";
    }

    public void Delete(string url)
    {
        var match = UploadedUrl().Match(url);
        if (!match.Success)
        {
            return;
        }

        var directory = Path.Combine(UploadsPath, "products", match.Groups[1].Value);
        foreach (var width in Widths)
        {
            File.Delete(Path.Combine(directory, $"{match.Groups[2].Value}-{width}.webp"));
        }
    }

    /// <summary>Yalnız depo biçimindeki adres silinebilir; klasör ve dosya adı sunucunun ürettiği biçimde olmalı.</summary>
    [GeneratedRegex(@"^/uploads/products/(\d+)/([0-9a-f]{32})-\d+\.webp$")]
    private static partial Regex UploadedUrl();
}
