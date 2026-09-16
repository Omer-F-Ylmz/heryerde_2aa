using System.Text;

namespace HerYerde.Web.Infrastructure;

/// <summary>Yüklenen videonun türü içerikten okunur; uzantı ve Content-Type istemciden geldiği için güvenilmez.</summary>
public static class VideoFile
{
    public const long MaxBytes = 80L * 1024 * 1024;

    /// <summary>Kaynak en fazla bu kadar sürebilir; daha uzunu hem yeniden kodlamayı hem sayfayı ağırlaştırır.</summary>
    public const int MaxDurationSeconds = 90;

    /// <summary>mp4 ailesinin ftyp markaları; "qt  " QuickTime (mov) demektir.</summary>
    private static readonly string[] Mp4Brands =
        ["isom", "iso2", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "mmp4", "M4V ", "dash"];

    /// <summary>Dosyayı kabul etmeyen ilk kural; sorun yoksa null. Süre burada bakılmaz: ffprobe gerektirir.</summary>
    public static async Task<string?> ProblemAsync(IFormFile file, CancellationToken cancellationToken)
    {
        // Dosya adı hiçbir yerde yol olarak kullanılmıyor; yine de yol taşıyan istek baştan reddedilir.
        if (file.FileName.Length == 0 || Path.GetFileName(file.FileName) != file.FileName)
        {
            return "Dosya adı geçersiz.";
        }

        if (file.Length <= 0 || file.Length > MaxBytes)
        {
            return "Video 80 MB'tan büyük olamaz.";
        }

        var head = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken);

        return Kind(head.AsSpan(0, read)) is null ? "Yalnız mp4 ve mov yüklenebilir." : null;
    }

    /// <summary>Sihirli baytlardan tür: mp4, mov; tanınmayan içerik null.</summary>
    public static string? Kind(ReadOnlySpan<byte> head)
    {
        if (head.Length < 12 || !head[4..8].SequenceEqual("ftyp"u8))
        {
            return null;
        }

        var brand = Encoding.ASCII.GetString(head[8..12]);
        return brand == "qt  " ? "mov" : Array.IndexOf(Mp4Brands, brand) >= 0 ? "mp4" : null;
    }
}
