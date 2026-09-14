using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

public sealed record RefreshSummary(int Regenerated, int FromSource, int Skipped);

/// <summary>Yüklenmiş görselleri kare dolgu rengi değiştiğinde yeniden üretir; adresler korunur, veritabanına dokunulmaz.</summary>
public static class RefreshImagesCommand
{
    public const string Argument = "--gorsel-yenile";

    public static bool Requested(string[] args) => Array.IndexOf(args, Argument) >= 0;

    /// <summary>Bayraktan sonraki değer ham klasördür; verilmezse görseller depodaki 1200'lükten üretilir.</summary>
    public static string? DirectoryFrom(string[] args)
    {
        var index = Array.IndexOf(args, Argument);
        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }

        var next = args[index + 1];
        return next.StartsWith("--", StringComparison.Ordinal) ? null : next;
    }

    public static async Task<RefreshSummary> RunAsync(
        IServiceProvider services,
        string? rawDirectory,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var storage = scope.ServiceProvider.GetRequiredService<IProductImageStorage>();

        var sources = SourcesByProduct(storage.UploadsPath, rawDirectory);
        var (_, products) = await productService.GetAllAsync(cancellationToken);
        var (_, images) = await productService.GetImagesForAsync(products.Data!.Select(p => p.Id).ToList(), cancellationToken);

        var regenerated = 0;
        var fromSource = 0;
        var skipped = 0;

        foreach (var image in images.Data!)
        {
            // Ham dosya ürüne bağlıdır, görsele değil; yalnız birincil görsel kaynağından üretilir, kalanı 1200'lükten.
            var source = image.IsPrimary && sources.TryGetValue(image.ProductId, out var path) ? path : null;
            await using var content = source is null ? null : File.OpenRead(source);

            if (await storage.RegenerateAsync(image.Url, content, cancellationToken))
            {
                regenerated++;
                if (source is not null)
                {
                    fromSource++;
                }
            }
            else
            {
                skipped++;
            }
        }

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Görsel yenileme: {regenerated} yeniden üretildi ({fromSource} ham kaynaktan), {skipped} atlandı."));

        return new RefreshSummary(regenerated, fromSource, skipped);
    }

    /// <summary>.ithal.json hash→ürün eşlemesini ham klasördeki dosyalarla birleştirir; bir ürüne birden çok dosya
    /// düşerse hiçbiri seçilmez, o ürün 1200'lükten üretilir.</summary>
    private static Dictionary<int, string> SourcesByProduct(string uploadsPath, string? rawDirectory)
    {
        var statePath = Path.Combine(uploadsPath, ".ithal.json");
        if (rawDirectory is null || !Directory.Exists(rawDirectory) || !File.Exists(statePath))
        {
            return [];
        }

        var state = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(statePath)) ?? [];
        var sources = new Dictionary<int, string>();
        var ambiguous = new HashSet<int>();

        foreach (var file in Directory.EnumerateFiles(rawDirectory))
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
            if (!state.TryGetValue(hash, out var productId))
            {
                continue;
            }

            if (!sources.TryAdd(productId, file))
            {
                ambiguous.Add(productId);
            }
        }

        foreach (var productId in ambiguous)
        {
            sources.Remove(productId);
        }

        return sources;
    }
}
