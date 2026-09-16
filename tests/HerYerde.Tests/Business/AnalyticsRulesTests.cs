using HerYerde.Business.Rules;

namespace HerYerde.Tests.Business;

/// <summary>D16 A1: çerezsiz analitiğin saf kuralları — cihaz sınıfı, bot süzgeci, yönlendiren alan adı, UTM, izlenen yol.</summary>
public sealed class AnalyticsRulesTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1", "mobil")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Mobile Safari/537.36", "mobil")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36", "tablet")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1", "tablet")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36", "masaustu")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15", "masaustu")]
    public void Cihaz_sinifi_tarayici_kimliginden_cikar(string userAgent, string device)
        => Assert.Equal(device, AnalyticsRules.DeviceOf(userAgent));

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
    [InlineData("Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)")]
    [InlineData("facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)")]
    [InlineData("Mozilla/5.0 (compatible; AhrefsBot/7.0; +http://ahrefs.com/robot/)")]
    [InlineData("WhatsApp/2.23.20.0")]
    [InlineData("curl/8.4.0")]
    [InlineData("python-requests/2.31.0")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/128.0 Safari/537.36")]
    [InlineData("")]
    [InlineData(null)]
    public void Bot_ve_bos_tarayici_kimligi_sayilmaz(string? userAgent)
        => Assert.True(AnalyticsRules.IsBot(userAgent));

    [Fact]
    public void Gercek_tarayici_bot_sayilmaz()
        => Assert.False(AnalyticsRules.IsBot("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1"));

    [Theory]
    [InlineData("https://www.instagram.com/p/abc", "instagram.com")]
    [InlineData("https://l.instagram.com/?u=https%3A%2F%2Fheryerde.com", "instagram.com")]
    [InlineData("https://m.facebook.com/", "facebook.com")]
    [InlineData("https://www.google.com.tr/", "google.com.tr")]
    [InlineData("https://heryerde.com/ev", null)]
    [InlineData("https://www.heryerde.com/ev", null)]
    [InlineData("android-app://com.google.android.gm/", null)]
    [InlineData("yazı", null)]
    [InlineData(null, null)]
    public void Yonlendiren_alan_adi_sadelesir_site_ici_gecis_kaynak_sayilmaz(string? referer, string? host)
        => Assert.Equal(host, AnalyticsRules.ReferrerHost(referer, "heryerde.com"));

    [Theory]
    [InlineData(" Instagram ", "instagram")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Utm_degeri_kucuk_harf_ve_kirpilmis_yazilir(string? raw, string? expected)
        => Assert.Equal(expected, AnalyticsRules.Utm(raw));

    [Fact]
    public void Uzun_utm_degeri_60_karakterde_kesilir()
        => Assert.Equal(60, AnalyticsRules.Utm(new string('a', 100))!.Length);

    [Theory]
    [InlineData("/", true)]
    [InlineData("/ev/tencere-tava", true)]
    [InlineData("/urun/dokum-tava", true)]
    [InlineData("/sepet", true)]
    [InlineData("/admin/analitik", false)]
    [InlineData("/health", false)]
    [InlineData("/health/ready", false)]
    [InlineData("/feeds/google.xml", false)]
    [InlineData("/olay", false)]
    [InlineData("/sw.js", false)]
    [InlineData("/ara/oner", false)]
    [InlineData("/siparis/HY-20260115-0001/tesekkur", false)]
    [InlineData("/cevrimdisi", false)]
    public void Izlenen_yollar_yonetim_saglik_akis_ve_siparis_sayfalarini_disarida_birakir(string path, bool tracked)
        => Assert.Equal(tracked, AnalyticsRules.IsTrackedPath(path));
}
