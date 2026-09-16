namespace HerYerde.Tests.Web;

/// <summary>D13 B5: kalıcı CI zap-scan haftalık oturumlu yönetim taramasını da koşar: tarama hesabı çerezi, Automation Framework planı
/// (aktif tarama 40 dk tavan, doğrulanmış yanlış pozitif filtresi, yıkıcı uçlar dışarıda), oturum kanıtı ve Medium+ kapısı.</summary>
public sealed class ZapAdminScanTests
{
    [Fact]
    public void Ci_zap_scan_oturumlu_yonetim_taramasini_haftalik_kosar()
    {
        var ci = RepoFile.ReadAllText(".github", "workflows", "ci.yml");
        var job = ci[ci.IndexOf("  zap-scan:", StringComparison.Ordinal)..ci.IndexOf("  restore-check:", StringComparison.Ordinal)];

        Assert.Contains("github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'", job);
        Assert.Contains("bash .zap/yonetim-cerezi.sh", job);
        Assert.Contains("-autorun /zap/wrk/yonetim.yaml", job);
        Assert.Contains("GET /admin/rapor responded 200", job);
        Assert.Contains("False Positive", job);
        Assert.Contains("yonetim-full.json", job);
    }

    [Fact]
    public void Yonetim_plani_40_dakika_tavanli_yanlis_pozitif_filtreli_yikici_uclari_disarida_birakir()
    {
        var plan = RepoFile.ReadAllText(".zap", "yonetim.yaml");

        Assert.Contains("maxScanDurationInMins: 40", plan);
        Assert.Contains("newRisk: \"False Positive\"", plan);
        // D14/D15: yeni silme uçları Türkçe adlandığı için [Dd]elete kalıbına girmez, ayrıca yazılır.
        foreach (var excluded in new[]
                 {
                     "/admin/auth/logout", "/admin/sifre", "/admin/orders/anonymize", "/admin/kvkk/anonimlestir",
                     "/admin/duyurular/[0-9]+/sil", "/admin/kuponlar/[0-9]+/sil", "/admin/ceyizlistesi/[0-9]+/sil", "/admin/markalar/[0-9]+/sil"
                 })
        {
            Assert.Contains(excluded, plan);
        }

        Assert.True(File.Exists(RepoFile.PathOf(".zap", "yonetim-cerezi.sh")));
    }
}
