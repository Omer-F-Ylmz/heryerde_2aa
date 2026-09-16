using System.Text.RegularExpressions;
using HerYerde.Web.Infrastructure;

namespace HerYerde.Tests.Web;

/// <summary>ZAP-1 kalanları: prod compose alan adı süzgecini zorunlu ortam değişkeninden alır;
/// sağlık denetimi imajda olmayan wget yerine uygulamanın kendi dll'iyle çalışır.</summary>
public sealed class DeploymentConfigTests
{
    [Fact]
    public void Prod_compose_AllowedHosts_zorunlu_ortam_degiskeninden_okur()
    {
        var compose = RepoFile.ReadAllText("docker-compose.prod.yml").ReplaceLineEndings("\n");

        Assert.Contains("\n      AllowedHosts: ${HERYERDE_ALLOWED_HOSTS:?", compose);
        Assert.DoesNotContain("AllowedHosts", RepoFile.ReadAllText("docker-compose.zap.yml"));
        Assert.Contains("HERYERDE_ALLOWED_HOSTS: \"localhost\"", RepoFile.ReadAllText(".github", "workflows", "ci.yml"));
    }

    [Fact]
    public void Prod_compose_saglik_denetimi_imajdaki_dotnet_ile_calisir()
    {
        var compose = RepoFile.ReadAllText("docker-compose.prod.yml");

        Assert.DoesNotContain("wget", compose);
        Assert.Contains("test: [\"CMD\", \"dotnet\", \"HerYerde.Web.dll\", \"--healthcheck\"]", compose);
    }

    private static readonly string[] OptionalComposeVariables =
        ["HERYERDE_SMTP_PORT", "HERYERDE_ETBIS_NO", "HERYERDE_IYZICO_INSTALLMENTS", "HERYERDE_WEB_IMAGE"];

    /// <summary>YAYIN-HAZIRLIK: dış hesap değerlerinden biri eksikse "docker compose up" yorumlama aşamasında durur.</summary>
    [Fact]
    public void Prod_compose_zorunlu_env_eksikse_ayaga_kalkmaz()
    {
        var compose = RepoFile.ReadAllText("docker-compose.prod.yml").ReplaceLineEndings("\n");

        foreach (var line in new[]
                 {
                     "Iyzico__ApiKey: ${HERYERDE_IYZICO_API_KEY:?",
                     "Iyzico__SecretKey: ${HERYERDE_IYZICO_SECRET_KEY:?",
                     "Iyzico__BaseUrl: ${HERYERDE_IYZICO_BASE_URL:?",
                     "Iyzico__CspSources__0: ${HERYERDE_IYZICO_CSP_SOURCE:?",
                     "Notifications__Host: ${HERYERDE_SMTP_HOST:?",
                     "Notifications__User: ${HERYERDE_SMTP_USER:?",
                     "Notifications__Password: ${HERYERDE_SMTP_PASSWORD:?",
                     "Notifications__From: ${HERYERDE_SMTP_FROM:?",
                     "Notifications__StoreTo: ${HERYERDE_STORE_EMAIL:?",
                     "Sentry__Dsn: ${HERYERDE_SENTRY_DSN:?",
                     "Shop__BaseUrl: ${HERYERDE_BASE_URL:?",
                     "AllowedHosts: ${HERYERDE_ALLOWED_HOSTS:?",
                     "Legal__EtbisNo: ${HERYERDE_ETBIS_NO:-}"
                 })
        {
            Assert.Contains(line, compose);
        }

        var references = Regex.Matches(compose, @"\$\{(HERYERDE_[A-Z_]+)(:\?|:-)");
        Assert.NotEmpty(references);
        Assert.All(references, match => Assert.True(
            match.Groups[2].Value == ":?" || OptionalComposeVariables.Contains(match.Groups[1].Value),
            match.Value + " zorunlu değil"));
    }

    [Fact]
    public void Prod_compose_degiskenleri_dis_hesaplar_ile_birebir()
    {
        static SortedSet<string> Variables(string text)
            => new(Regex.Matches(text, "HERYERDE_[A-Z_]+[A-Z]").Select(m => m.Value));

        Assert.Equal(
            Variables(RepoFile.ReadAllText("docker-compose.prod.yml")),
            Variables(RepoFile.ReadAllText("docs", "dis-hesaplar.md")));
    }

    [Fact]
    public void Dockerfile_cok_asamali_root_olmayan_saglik_denetimli_ve_ithal_tablolarini_tasir()
    {
        var dockerfile = RepoFile.ReadAllText("Dockerfile").ReplaceLineEndings("\n");

        Assert.Contains("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build", dockerfile);
        Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime", dockerfile);
        Assert.Contains("\nUSER $APP_UID\n", dockerfile);
        Assert.Contains("HEALTHCHECK", dockerfile);
        Assert.Contains("CMD [\"dotnet\", \"HerYerde.Web.dll\", \"--healthcheck\"]", dockerfile);
        // --ithal varsayılan tabloyu depo köküne göre arar; imajda kök /app.
        Assert.Contains("COPY docs/ithal-1.md docs/ithal-2.md ./docs/", dockerfile);
        Assert.Contains("/app/wwwroot/uploads", dockerfile);
    }

    [Fact]
    public void Prod_compose_gecelik_yedek_servisi_ve_kalici_uploads_tasir()
    {
        var compose = RepoFile.ReadAllText("docker-compose.prod.yml");
        var ci = RepoFile.ReadAllText(".github", "workflows", "ci.yml");

        Assert.Contains("  backup:", compose);
        Assert.Contains("dotnet HerYerde.Web.dll --yedek-al /backups", compose);
        Assert.Contains("- ./backups:/backups", compose);
        Assert.Contains("- heryerde-uploads:/app/wwwroot/uploads", compose);
        // CI: yedek klasörü (10001:1654, 0770) build context'e girerse imaj derlemesi "permission denied" ile düşer.
        Assert.Contains("backups/", RepoFile.ReadAllText(".dockerignore"));
        // "docker compose up --wait" sağlık denetimi olmayan servisi hata sayar.
        Assert.DoesNotContain("disable: true", compose);
        Assert.Contains("  restore-check:", ci);
        Assert.Contains("  smoke:", ci);
        Assert.Contains("tools/smoke.ps1", ci);
    }

    [Theory]
    [InlineData("heryerde.com;www.heryerde.com", "heryerde.com")]
    [InlineData("*.heryerde.com;heryerde.com", "heryerde.com")]
    [InlineData("*", "localhost")]
    [InlineData(null, "localhost")]
    public void Saglik_denetimi_izinli_ilk_alan_adiyla_istek_atar(string? allowedHosts, string expected)
        => Assert.Equal(expected, HealthProbe.HostFor(allowedHosts));
}
