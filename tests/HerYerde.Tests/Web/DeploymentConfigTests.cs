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

    [Theory]
    [InlineData("heryerde.com;www.heryerde.com", "heryerde.com")]
    [InlineData("*.heryerde.com;heryerde.com", "heryerde.com")]
    [InlineData("*", "localhost")]
    [InlineData(null, "localhost")]
    public void Saglik_denetimi_izinli_ilk_alan_adiyla_istek_atar(string? allowedHosts, string expected)
        => Assert.Equal(expected, HealthProbe.HostFor(allowedHosts));
}
