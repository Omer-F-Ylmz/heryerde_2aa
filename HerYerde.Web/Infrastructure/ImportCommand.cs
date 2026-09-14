using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Web.Infrastructure;

public sealed record ImportSummary(int Products, int Images, int Skipped);

/// <summary>Tek seferlik ham fotoğraf ithali: docs/ithal-1.md satırlarını ürüne ve işlenmiş görsele çevirir.</summary>
public static class ImportCommand
{
    public const string Argument = "--ithal";

    /// <summary>Fiyat sonradan girilir; 1 ₺ "fiyat eksik" bayrağıdır, ürün taslak kalır.</summary>
    public const decimal PlaceholderPrice = 1m;

    public static string? DirectoryFrom(string[] args)
    {
        var index = Array.IndexOf(args, Argument);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    public static async Task<ImportSummary> RunAsync(
        IServiceProvider services,
        string rawDirectory,
        string tableFile,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var categoryService = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var storage = scope.ServiceProvider.GetRequiredService<IProductImageStorage>();

        var statePath = Path.Combine(storage.UploadsPath, ".ithal.json");
        var state = ReadState(statePath);
        var (_, categories) = await categoryService.GetAllAsync(cancellationToken);
        var categoryIds = categories.Data!.ToDictionary(c => c.Slug, c => c.Id);

        var products = 0;
        var images = 0;
        var skipped = 0;

        foreach (var row in Rows(await File.ReadAllLinesAsync(tableFile, cancellationToken)))
        {
            var path = Path.Combine(rawDirectory, row.File);
            if (!File.Exists(path))
            {
                Console.WriteLine($"atlandı (dosya yok): {row.File}");
                skipped++;
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (state.ContainsKey(hash))
            {
                skipped++;
                continue;
            }

            if (!categoryIds.TryGetValue(row.Category, out var categoryId))
            {
                Console.WriteLine($"atlandı (kategori yok): {row.File} → {row.Category}");
                skipped++;
                continue;
            }

            var (_, stored) = await productService.GetAllAsync(cancellationToken);
            // Slug'ı ProductManager addan üretir; satırdaki slug yalnız aramada ipucu.
            var product = stored.Data!.FirstOrDefault(p => p.Slug == row.Slug || p.Name == row.Name);
            if (product is null)
            {
                var (status, created) = await productService.AddAsync(new Product
                {
                    Name = row.Name,
                    Description = row.Description,
                    CategoryId = categoryId,
                    Price = PlaceholderPrice,
                    IsActive = false
                }, cancellationToken);

                if (status != HttpStatusCode.Created)
                {
                    Console.WriteLine($"atlandı ({created.Message}): {row.File}");
                    skipped++;
                    continue;
                }

                product = created.Data!;
                products++;
            }

            var (_, existing) = await productService.GetImagesAsync(product.Id, cancellationToken);
            await using var content = new MemoryStream(bytes);
            var url = await storage.SaveAsync(product.Id, content, cancellationToken);
            await productService.AddImageAsync(new ProductImage
            {
                ProductId = product.Id,
                Url = url,
                Alt = row.Name,
                SortOrder = existing.Data!.Count,
                IsPrimary = existing.Data!.Count == 0
            }, cancellationToken);

            state[hash] = product.Id;
            images++;
        }

        WriteState(statePath, state);
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"İthal: {products} ürün, {images} görsel, {skipped} atlanan."));

        return new ImportSummary(products, images, skipped);
    }

    private sealed record Row(string File, string Name, string Slug, string Category, string Description);

    /// <summary>Markdown tablosu: başlık ve ayraç satırı atlanır, ilk beş sütun okunur.</summary>
    private static IEnumerable<Row> Rows(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('|'))
            {
                continue;
            }

            var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length < 5 || cells[0] is "dosya" || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            yield return new Row(cells[0], cells[1], cells[2], cells[3], cells[4]);
        }
    }

    private static Dictionary<string, int> ReadState(string path)
        => File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path)) ?? []
            : [];

    private static void WriteState(string path, Dictionary<string, int> state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(state));
    }
}
