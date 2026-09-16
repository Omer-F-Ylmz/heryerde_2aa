namespace HerYerde.Tests.Web;

/// <summary>D16 C: sürekli yayın — .github/workflows/deploy.yml iki hedef (repo değişkeni DEPLOY_TARGET: plesk | compose), staging CI
/// yeşilinde otomatik, prod elle ve ortam korumalı; migration yayın adımında, yayın sonrası smoke, hata olursa geri alma.
/// Workflow dosyaları CI'da actionlint ile doğrulanır.</summary>
public sealed class DeployWorkflowTests
{
    private static string Workflow => RepoFile.ReadAllText(".github", "workflows", "deploy.yml").ReplaceLineEndings("\n");

    /// <summary>İş (job) bloğu: "  ad:" satırından bir sonraki iki boşluk girintili iş adına kadar.</summary>
    private static string Job(string name)
    {
        var text = Workflow;
        var start = text.IndexOf("\n  " + name + ":\n", StringComparison.Ordinal);
        Assert.True(start >= 0, name + " işi yok");
        var next = System.Text.RegularExpressions.Regex.Match(text[(start + 1)..], "\n  [a-z-]+:\n");
        return next.Success ? text[start..(start + 1 + next.Index)] : text[start..];
    }

    [Fact]
    public void Ci_workflow_dosyalarini_actionlint_ile_dogrular()
    {
        var ci = RepoFile.ReadAllText(".github", "workflows", "ci.yml");
        var buildTest = ci[ci.IndexOf("  build-test:", StringComparison.Ordinal)..ci.IndexOf("  browser-check:", StringComparison.Ordinal)];

        Assert.Contains("rhysd/actionlint:", buildTest);
        Assert.True(File.Exists(RepoFile.PathOf(".github", "workflows", "deploy.yml")));
    }

    [Fact]
    public void Hedef_DEPLOY_TARGET_repo_degiskeniyle_secilir()
    {
        Assert.Contains("vars.DEPLOY_TARGET == 'plesk'", Job("plesk"));
        Assert.Contains("vars.DEPLOY_TARGET == 'compose'", Job("compose"));
        Assert.Contains("needs: plan", Job("plesk"));
        Assert.Contains("needs: plan", Job("compose"));
        Assert.Contains("DEPLOY_TARGET tanımsız", Job("plan"));
    }

    [Fact]
    public void Staging_ci_main_pushunda_yesilse_otomatik_prod_elle_ve_ortam_korumali()
    {
        var workflow = Workflow;
        var plan = Job("plan");

        Assert.Contains("workflow_run:\n    workflows: [CI]\n    types: [completed]\n    branches: [main]", workflow);
        Assert.Contains("github.event.workflow_run.conclusion == 'success'", plan);
        Assert.Contains("github.event.workflow_run.event == 'push'", plan);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("options: [staging, production]", workflow);
        foreach (var job in new[] { Job("plesk"), Job("compose") })
        {
            Assert.Contains("environment:\n      name: ${{ needs.plan.outputs.environment }}", job);
        }
    }

    [Fact]
    public void Migration_yayin_adiminda_dosyalardan_once_uygulanir()
    {
        foreach (var job in new[] { Job("plesk"), Job("compose") })
        {
            var migrate = job.IndexOf("--migrate", StringComparison.Ordinal);
            var deploy = job.IndexOf("- name: Deploy", StringComparison.Ordinal);
            Assert.True(migrate >= 0, "migration adımı yok");
            Assert.True(deploy > migrate, "migration yayından sonra");
        }
    }

    [Fact]
    public void Yayin_sonrasi_smoke_kosar_staging_kapisi_kimlikle_gecilir()
    {
        foreach (var job in new[] { Job("plesk"), Job("compose") })
        {
            var deploy = job.IndexOf("- name: Deploy", StringComparison.Ordinal);
            var smoke = job.IndexOf("- name: Smoke", StringComparison.Ordinal);
            Assert.True(smoke > deploy, "smoke yayından önce ya da yok");
            Assert.Contains("tools/smoke.ps1", job[smoke..]);
            Assert.Contains("-Credential", job[smoke..]);
            Assert.Contains("-Staging", job[smoke..]);
        }
    }

    [Fact]
    public void Hata_olursa_geri_alma_adimi_tanimli()
    {
        var plesk = Job("plesk");
        var compose = Job("compose");

        foreach (var job in new[] { plesk, compose })
        {
            var rollback = job.IndexOf("- name: Rollback", StringComparison.Ordinal);
            Assert.True(rollback > job.IndexOf("- name: Smoke", StringComparison.Ordinal), "geri alma adımı yok");
            Assert.Contains("if: failure() && steps.migrate.outcome == 'success'", job[rollback..]);
        }

        // Plesk: son başarılı yayının etiketi yeniden yayımlanır; compose: sunucudaki önceki imaj etiketine dönülür.
        Assert.Contains("yayin/", plesk);
        Assert.Contains("HERYERDE_WEB_IMAGE", compose);
    }

    [Fact]
    public void Staging_compose_eki_ortami_kapiyi_sandboxu_ve_smtp_yoklugunu_ayarlar()
    {
        var staging = RepoFile.ReadAllText("docker-compose.staging.yml").ReplaceLineEndings("\n");
        var prod = RepoFile.ReadAllText("docker-compose.prod.yml").ReplaceLineEndings("\n");

        Assert.Contains("ASPNETCORE_ENVIRONMENT: Staging", staging);
        Assert.Contains("StagingGate__User: ${STAGING_USER:?", staging);
        Assert.Contains("StagingGate__Password: ${STAGING_PASS:?", staging);
        Assert.Contains("Notifications__Host: \"\"", staging);
        Assert.Contains("Iyzico__BaseUrl: https://sandbox-api.iyzipay.com", staging);
        Assert.Contains("image: ${HERYERDE_WEB_IMAGE:-heryerde-web:latest}", prod);
    }

    [Fact]
    public void Yayin_kontrol_listesi_cd_ve_staging_bolumunu_iki_hedef_sutunuyla_anlatir()
    {
        var doc = RepoFile.ReadAllText("docs", "yayin-kontrol.md");

        Assert.Contains("## Sürekli yayın (CD) ve staging", doc);
        Assert.Contains("| Adım | Plesk / runasp.net (`DEPLOY_TARGET=plesk`) | Compose / SSH (`DEPLOY_TARGET=compose`) |", doc);
        foreach (var name in new[] { "STAGING_USER", "STAGING_PASS", "--migrate", "smoke.ps1", "Rollback", "production" })
        {
            Assert.Contains(name, doc);
        }
    }
}
