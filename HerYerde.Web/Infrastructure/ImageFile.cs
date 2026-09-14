namespace HerYerde.Web.Infrastructure;

/// <summary>Yüklenen dosyanın türü içerikten okunur; uzantı ve Content-Type istemciden geldiği için güvenilmez.</summary>
public static class ImageFile
{
    public const long MaxBytes = 8L * 1024 * 1024;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Dosyayı kabul etmeyen ilk kural; sorun yoksa null.</summary>
    public static async Task<string?> ProblemAsync(IFormFile file, CancellationToken cancellationToken)
    {
        // Dosya adı hiçbir yerde yol olarak kullanılmıyor; yine de yol taşıyan istek baştan reddedilir.
        if (file.FileName.Length == 0 || Path.GetFileName(file.FileName) != file.FileName)
        {
            return "Dosya adı geçersiz.";
        }

        if (file.Length <= 0 || file.Length > MaxBytes)
        {
            return "Görsel 8 MB'tan büyük olamaz.";
        }

        var head = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken);

        return Kind(head.AsSpan(0, read)) is null ? "Yalnız jpg, png ve webp yüklenebilir." : null;
    }

    /// <summary>Sihirli baytlardan tür: jpeg, png, webp; tanınmayan içerik null.</summary>
    public static string? Kind(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return "jpeg";
        }

        if (head.Length >= 8 && head[..8].SequenceEqual(PngSignature))
        {
            return "png";
        }

        return head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8)
            ? "webp"
            : null;
    }
}
