using System.Text.RegularExpressions;

namespace HerYerde.Web.Infrastructure;

/// <summary>Fatura ve dekont gibi kişisel belgeler: wwwroot dışında durur, statik dosya olarak sunulmaz; yalnız
/// yetki denetleyen eylemler okur. Kayıtta göreli yol ("faturalar/{guid}.pdf") tutulur.</summary>
public interface IPrivateFileStorage
{
    /// <summary>Gizli deponun kök klasörü; gecelik yedek buradan "belgeler" arşivi alır.</summary>
    string RootPath { get; }

    Task<string> SaveAsync(string folder, string extension, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Adı sunucunun belirlediği belgeyi (sözleşme arşivi) yazar; önce geçici dosyaya, sonra yerine taşır.</summary>
    Task WriteAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Göreli yol depo biçimindeyse ve dosya varsa tam yol; değilse null.</summary>
    string? Resolve(string? relativePath);

    void Delete(string? relativePath);

    /// <summary>Klasördeki verilen yaştan eski dosyaları siler (onaylanmamış içe aktarma önizlemeleri).</summary>
    void DeleteOlderThan(string folder, TimeSpan age, DateTime utcNow);
}

public sealed partial class PrivateFileStorage(string root) : IPrivateFileStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public string RootPath => root;

    public async Task<string> SaveAsync(string folder, string extension, byte[] content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.Combine(root, folder));
        var relative = $"{folder}/{Guid.NewGuid():n}.{extension}";
        await File.WriteAllBytesAsync(Path.Combine(root, relative), content, cancellationToken);
        return relative;
    }

    public async Task WriteAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default)
    {
        var target = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + "." + Guid.NewGuid().ToString("n") + ".tmp";
        await File.WriteAllBytesAsync(temporary, content, cancellationToken);
        File.Move(temporary, target, overwrite: true);
    }

    public string? Resolve(string? relativePath)
        => relativePath is not null && StoredPath().IsMatch(relativePath) && File.Exists(Path.Combine(root, relativePath))
            ? Path.Combine(root, relativePath)
            : null;

    public void Delete(string? relativePath)
    {
        if (Resolve(relativePath) is { } path)
        {
            File.Delete(path);
        }
    }

    public void DeleteOlderThan(string folder, TimeSpan age, DateTime utcNow)
    {
        var directory = Path.Combine(root, folder);
        foreach (var file in Directory.Exists(directory) ? Directory.GetFiles(directory) : [])
        {
            if (File.GetLastWriteTimeUtc(file) < utcNow - age)
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>Uzantı dosya adından değil içerikten: PDF, PNG, JPEG, WebP imzası; başka her şey null (çalıştırılabilir dahil).</summary>
    public static string? Kind(byte[] content)
    {
        static bool StartsWith(byte[] bytes, int offset, params byte[] signature)
            => bytes.Length >= offset + signature.Length && bytes.AsSpan(offset, signature.Length).SequenceEqual(signature);

        return StartsWith(content, 0, "%PDF-"u8.ToArray()) ? "pdf"
            : StartsWith(content, 0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) ? "png"
            : StartsWith(content, 0, 0xFF, 0xD8, 0xFF) ? "jpg"
            : StartsWith(content, 0, "RIFF"u8.ToArray()) && StartsWith(content, 8, "WEBP"u8.ToArray()) ? "webp"
            : null;
    }

    public static string ContentType(string relativePath) => Path.GetExtension(relativePath) switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        _ => "image/webp"
    };

    /// <summary>Yalnız sunucunun ürettiği biçim: klasör/32 hex.uzantı ya da sözleşme arşivinde sürüm adı; "../" gibi yollar geçmez.</summary>
    [GeneratedRegex(@"^((faturalar|dekontlar|aktarimlar|iadeler)/[0-9a-f]{32}\.(pdf|png|jpg|webp|xlsx)|sozlesmeler/\d{4}-\d{2}-\d{2}(\.\d+)?\.pdf)$")]
    private static partial Regex StoredPath();
}
