using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>PWA (D16): /sw.js service worker'ı ve markalı çevrimdışı sayfası. Service worker'ın sürüm anahtarı site.css ve site.js
/// içeriğinin özetidir: varlık değişince yeni sürüm kurulur, eski önbellekler silinir.</summary>
public class PwaController(IWebHostEnvironment environment) : Controller
{
    private static readonly Lock VersionLock = new();
    private static string? _version;

    [HttpGet("sw.js")]
    public IActionResult ServiceWorker()
    {
        // Tarayıcı her sayfa açılışında güncelliğini sorar; önbellekten eski service worker kalmasın.
        Response.Headers.CacheControl = "no-cache";
        return Content(ServiceWorkerScript.Source.Replace("{{VERSION}}", Version(), StringComparison.Ordinal), "text/javascript");
    }

    [HttpGet("cevrimdisi")]
    public IActionResult Offline() => View();

    private string Version()
    {
        lock (VersionLock)
        {
            if (_version is null)
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                foreach (var path in new[] { "css/site.css", "js/site.js" })
                {
                    using var stream = environment.WebRootFileProvider.GetFileInfo(path).CreateReadStream();
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    hash.AppendData(buffer.ToArray());
                }

                _version = Convert.ToHexStringLower(hash.GetHashAndReset())[..12];
            }

            return _version;
        }
    }
}
