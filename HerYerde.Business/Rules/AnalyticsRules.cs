namespace HerYerde.Business.Rules;

/// <summary>Analitik olay adları. Huni adımları yalnız sunucudan yazılır; tarayıcıdan (beacon) yalnız tıklamalar kabul edilir.</summary>
public static class AnalyticsEvent
{
    public const string View = "goruntuleme";
    public const string AddToCart = "sepet";
    public const string Checkout = "odeme";
    public const string Order = "siparis";
    public const string Search = "arama";
    public const string WhatsApp = "whatsapp";
    public const string Instagram = "instagram";

    public static readonly IReadOnlySet<string> ClientEvents = new HashSet<string>(StringComparer.Ordinal) { WhatsApp, Instagram };
}

/// <summary>Çerezsiz analitiğin saf kuralları (D16): bot süzgeci, cihaz sınıfı, yönlendiren alan adı, UTM ve izlenen yollar.</summary>
public static class AnalyticsRules
{
    public const int PathLength = 200;
    public const int HostLength = 100;
    public const int UtmLength = 60;

    public const string Mobile = "mobil";
    public const string Tablet = "tablet";
    public const string Desktop = "masaustu";

    /// <summary>Tarayıcı kimliğinde geçerse bot/araç sayılır. WhatsApp ve Telegram bağlantı önizleme alıcılarıdır; Instagram'ın
    /// uygulama içi tarayıcısı ("Instagram 3xx") bot değildir.</summary>
    private static readonly string[] BotMarkers =
    [
        "bot", "crawl", "spider", "slurp", "facebookexternalhit", "whatsapp", "telegram", "preview", "headless", "lighthouse",
        "curl", "wget", "python", "java/", "go-http", "okhttp", "httpclient", "axios", "node-fetch", "pingdom", "uptime"
    ];

    /// <summary>Sayılmayan yollar: yönetim, sağlık, ürün akışları, beacon, service worker, öneri parçası, sipariş/teşekkür ve
    /// anahtarlı çeyiz yönetimi, çevrimdışı ve hata sayfaları, 3D dönüşü.</summary>
    private static readonly string[] ExcludedPaths =
    [
        "/admin", "/health", "/feeds", "/olay", "/sw.js", "/ara/oner", "/siparis", "/ceyizlistesi/yonet", "/cevrimdisi", "/hata",
        "/odeme/3d-donus"
    ];

    private static readonly string[] HostPrefixes = ["www.", "l.", "lm.", "m."];

    public static bool IsBot(string? userAgent)
        => string.IsNullOrWhiteSpace(userAgent) || BotMarkers.Any(marker => userAgent.Contains(marker, StringComparison.OrdinalIgnoreCase));

    public static string DeviceOf(string? userAgent)
    {
        var agent = userAgent ?? string.Empty;
        if (agent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || agent.Contains("Tablet", StringComparison.OrdinalIgnoreCase)
            || (agent.Contains("Android", StringComparison.OrdinalIgnoreCase) && !agent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)))
        {
            return Tablet;
        }

        return agent.Contains("Mobi", StringComparison.OrdinalIgnoreCase) || agent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            ? Mobile
            : Desktop;
    }

    /// <summary>Yönlendiren adresin alan adı, "www./l./m." öneki atılmış; sitenin kendisi (site içi geçiş) ve http(s) olmayan
    /// adres kaynak sayılmaz.</summary>
    public static string? ReferrerHost(string? referer, string ownHost)
    {
        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var host = Bare(uri.Host);
        if (host == Bare(ownHost))
        {
            return null;
        }

        return host.Length > HostLength ? host[..HostLength] : host;
    }

    public static string? Utm(string? value)
    {
        var trimmed = value?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > UtmLength ? trimmed[..UtmLength] : trimmed;
    }

    public static bool IsTrackedPath(string path)
        => !ExcludedPaths.Any(excluded => path.Equals(excluded, StringComparison.OrdinalIgnoreCase)
                                          || path.StartsWith(excluded + "/", StringComparison.OrdinalIgnoreCase));

    public static bool IsProductPath(string path) => path.StartsWith("/urun/", StringComparison.Ordinal);

    /// <summary>Gezilebilir kökler ve alt kategorileri: /ev, /ortu, /ev/{slug}, /ortu/{slug}.</summary>
    public static bool IsCategoryPath(string path)
        => path is "/ev" or "/ortu" || path.StartsWith("/ev/", StringComparison.Ordinal) || path.StartsWith("/ortu/", StringComparison.Ordinal);

    private static string Bare(string host)
    {
        var lower = host.ToLowerInvariant();
        foreach (var prefix in HostPrefixes)
        {
            if (lower.StartsWith(prefix, StringComparison.Ordinal))
            {
                return lower[prefix.Length..];
            }
        }

        return lower;
    }
}
