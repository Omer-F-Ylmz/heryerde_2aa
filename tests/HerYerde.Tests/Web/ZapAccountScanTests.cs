namespace HerYerde.Tests.Web;

/// <summary>D16: zap-scan üye hesabı yüzeyini (doğrulanmış tarama üyesinin oturumuyla /hesap sayfaları) ve analitik beacon ucunu
/// (/olay) da tarar; hesabı bozan uçlar (çıkış, silme, parola, oturum kapatma, adres silme) dışarıda, oturum kanıtı logdan.</summary>
public sealed class ZapAccountScanTests
{
    [Fact]
    public void Ci_zap_scan_uye_oturumunu_kurar_ve_oturumlu_tarandigini_kanitlar()
    {
        var ci = RepoFile.ReadAllText(".github", "workflows", "ci.yml");
        var job = ci[ci.IndexOf("  zap-scan:", StringComparison.Ordinal)..ci.IndexOf("  restore-check:", StringComparison.Ordinal)];

        Assert.Contains("bash .zap/uye-cerezi.sh", job);
        Assert.Contains("GET /hesap/yorumlar responded 200", job);
        Assert.True(File.Exists(RepoFile.PathOf(".zap", "uye-cerezi.sh")));
    }

    [Fact]
    public void Plan_uye_hesabi_ve_beacon_ucunu_tarar_yikici_uye_uclarini_disarida_birakir()
    {
        var plan = RepoFile.ReadAllText(".zap", "yonetim.yaml");
        var context = plan[plan.IndexOf("    - name: uye", StringComparison.Ordinal)..plan.IndexOf("  parameters:", StringComparison.Ordinal)];

        Assert.Contains("http://localhost:8080/hesap/ayarlar", context);
        Assert.Contains("\"http://localhost:8080/olay.*\"", context);
        foreach (var excluded in new[]
                 {
                     "/hesap/cikis", "/hesap/sil", "/hesap/parola", "/hesap/oturumlar/kapat", "/hesap/adresler/[0-9]+/sil"
                 })
        {
            Assert.Contains(excluded, context);
        }

        Assert.Contains("method: POST", plan);
        Assert.Contains("data: \"ad=whatsapp&yol=/\"", plan);
        Assert.Contains("context: uye", plan);
    }
}
