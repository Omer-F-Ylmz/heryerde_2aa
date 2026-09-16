using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HerYerde.Web.Infrastructure;

/// <summary>Yeniden kodlanmış videonun üç adresi ve kaynağın saniye cinsinden süresi.</summary>
public sealed record VideoAssets(string Url, string PosterUrl, string PreviewUrl, int Duration);

public interface IProductVideoStorage
{
    /// <summary>İçeriği geçici dosyaya alıp süresini ölçer; 90 saniyeden uzunsa hiçbir şey yazmadan null döner.
    /// Kısa ise 720p H.264 mp4, poster jpg ve 3 saniyelik sessiz webm önizleme üretir.</summary>
    Task<VideoAssets?> SaveAsync(int productId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Adresin üç dosyasını da siler; depo dışındaki adresler sessizce atlanır.</summary>
    void Delete(string url);
}

public sealed partial class FfmpegVideoStorage : IProductVideoStorage
{
    /// <summary>Kartta üzerine gelince oynayan önizlemenin uzunluğu.</summary>
    private const int PreviewSeconds = 3;

    private readonly string _root;
    private readonly string _ffmpeg;
    private readonly string _ffprobe;

    /// <summary>ffmpeg PATH'te değilse Media:FfmpegPath tam yolu verir; ffprobe aynı klasörde aranır.</summary>
    public FfmpegVideoStorage(string root, string? ffmpegPath = null)
    {
        _root = root;
        _ffmpeg = ffmpegPath is { Length: > 0 } path ? path : "ffmpeg";
        _ffprobe = _ffmpeg == "ffmpeg"
            ? "ffprobe"
            : Path.Combine(Path.GetDirectoryName(_ffmpeg) ?? string.Empty, "ffprobe" + Path.GetExtension(_ffmpeg));
    }

    public async Task<VideoAssets?> SaveAsync(int productId, Stream content, CancellationToken cancellationToken = default)
    {
        var source = Path.Combine(Path.GetTempPath(), "heryerde-video-" + Guid.NewGuid().ToString("n"));
        try
        {
            await using (var temp = File.Create(source))
            {
                await content.CopyToAsync(temp, cancellationToken);
            }

            if (await DurationAsync(source, cancellationToken) is not { } duration || duration > VideoFile.MaxDurationSeconds)
            {
                return null;
            }

            var id = productId.ToString(CultureInfo.InvariantCulture);
            var directory = Path.Combine(_root, "uploads", "videos", id);
            var name = Guid.NewGuid().ToString("n");
            Directory.CreateDirectory(directory);

            // Yüksekliği 720'ye indirir, küçük kaynağı büyütmez; faststart ilk baytta oynatmayı başlatır.
            await RunAsync(_ffmpeg, cancellationToken,
                "-hide_banner", "-loglevel", "error", "-y", "-i", source,
                "-vf", @"scale=-2:min(720\,ih)",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-pix_fmt", "yuv420p",
                "-movflags", "+faststart", "-c:a", "aac", "-b:a", "128k",
                Path.Combine(directory, name + "-720.mp4"));

            // Kapak karesi ortadan değil baştan biraz sonra: ilk kare çoğu videoda karanlık açılır.
            var at = Math.Min(1, duration / 2.0).ToString("0.###", CultureInfo.InvariantCulture);
            await RunAsync(_ffmpeg, cancellationToken,
                "-hide_banner", "-loglevel", "error", "-y", "-ss", at, "-i", source,
                "-frames:v", "1", "-vf", @"scale=-2:min(720\,ih)", "-q:v", "3",
                Path.Combine(directory, name + "-poster.jpg"));

            await RunAsync(_ffmpeg, cancellationToken,
                "-hide_banner", "-loglevel", "error", "-y", "-i", source,
                "-t", PreviewSeconds.ToString(CultureInfo.InvariantCulture), "-an",
                "-vf", @"scale=-2:min(360\,ih)",
                "-c:v", "libvpx-vp9", "-crf", "40", "-b:v", "0", "-deadline", "realtime", "-cpu-used", "5",
                Path.Combine(directory, name + "-onizleme.webm"));

            return new VideoAssets(
                $"/uploads/videos/{id}/{name}-720.mp4",
                $"/uploads/videos/{id}/{name}-poster.jpg",
                $"/uploads/videos/{id}/{name}-onizleme.webm",
                duration);
        }
        finally
        {
            File.Delete(source);
        }
    }

    public void Delete(string url)
    {
        var match = UploadedUrl().Match(url);
        if (!match.Success)
        {
            return;
        }

        var directory = Path.Combine(_root, "uploads", "videos", match.Groups[1].Value);
        foreach (var suffix in new[] { "-720.mp4", "-poster.jpg", "-onizleme.webm" })
        {
            File.Delete(Path.Combine(directory, match.Groups[2].Value + suffix));
        }
    }

    /// <summary>Kaynağın saniyeye yuvarlanmış süresi; ffprobe okuyamazsa (video değil, bozuk) null.</summary>
    private async Task<int?> DurationAsync(string source, CancellationToken cancellationToken)
    {
        using var process = Start(_ffprobe,
            "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", source);
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode == 0
               && double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
               && seconds > 0
            ? (int)Math.Round(seconds)
            : null;
    }

    private static async Task RunAsync(string program, CancellationToken cancellationToken, params string[] arguments)
    {
        using var process = Start(program, arguments);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(program)} başarısız ({process.ExitCode}): {error.Trim()}");
        }
    }

    private static Process Start(string program, params string[] arguments)
    {
        var info = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return Process.Start(info)!;
    }

    /// <summary>Yalnız depo biçimindeki adres silinebilir; klasör ve dosya adı sunucunun ürettiği biçimde olmalı.</summary>
    [GeneratedRegex(@"^/uploads/videos/(\d+)/([0-9a-f]{32})-720\.mp4$")]
    private static partial Regex UploadedUrl();
}
