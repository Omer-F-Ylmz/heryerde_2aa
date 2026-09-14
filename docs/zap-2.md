# ZAP-2 — OWASP ZAP otomatik tarama, D6-D9 sonrası (15 Eylül 2026)

Ortam: ZAP-1 ile aynı (`docker-compose.prod.yml` + `docker-compose.zap.yml`, Production, compose SQL Server, `AllowedHosts=localhost`,
GitHub Actions ubuntu, geçici `zap-tarama-2` dalı). Farklar:
- Prod'da katalog tohumu olmadığından tarama öncesi sqlcmd ile Ev kökü, bir alt kategori, bir ürün (`/urun/dokum-tava-28-cm`) ve
  onaylı yorum eklendi; böylece ürün sayfası, yorum formu, sepet ve ödeme yüzeyleri taranır (ZAP-1'de boş katalogdu).
- Yeni rotalar sitemap/örümcek ile dahil: `/iletisim` (form), `/sss`, `/hakkimizda`, `/urun/{slug}/yorum`. `/ortu` Giyim kökü
  tohumlanmadığı için 404 (beklenen).
- `.zap/vitrin.context`: `/admin.*` ve `/odeme/3d-donus.*` dışarıda (dönüş imzasız istekte siparişi iptal eder; imza/tekrar/tutar
  saldırıları kapanis-2.md §2'de elle sınandı).
- `docker-compose.zap.yml`: yeni kovalar (iletişim, yorum, 3D dönüşü) de yükseltildi; XFF artık sayılmadığından tarayıcı tek IP'dir.
- Baseline: `zap-baseline.py -a -j` + sepet çerezi replacer. Full: `zap-full-scan.py -n vitrin.context -j -m 5` + sepet çerezi +
  `rules.csrf.ignore.attname=data-no-csrf`.

İlk tarama (run 34899547359; Informational listelenmez):

ID | risk | uyarı adı | URL | karar
---|---|---|---|---
90022 | Low | Application Error Disclosure | POST http://localhost:8080/sepet/ekle (500) | DÜZELT — `donus` ASCII dışı karakter (ör. `/ş`, U+2028) taşıyınca `Url.IsLocalUrl` geçiyor, Kestrel Location başlığını reddedip 500 veriyordu; yalnız yazdırılabilir ASCII yerel adres kabul edilir (sepet + admin `returnUrl`). Test: CartQuantityTests.Ascii_disi_donus_adresi_sepete_yonlenir, AdminAuthorizationTests.Ascii_disi_return_url_urun_listesine_yonlenir
90004 | Low | Cross-Origin-Embedder-Policy Header Missing | http://localhost:8080/ | KABUL — ZAP-1 ile aynı gerekçe (`require-corp` dış ürün görsellerini kırar)

Informational (bilgi, karar gerekmez): 20012 `/iletisim` Anti-CSRF (formda token var; kural tokenı `data-no-csrf`'siz formlarda da
Informational sayar), 10112/10111 oturum ve giriş isteği tanıma, 10049/10050 önbellek, 10031 kullanıcı kontrollü öznitelik
(`?min=` fiyat süzgeci, `ReturnUrl` — Razor kaçışlı), 10104 UA fuzzer, 90005 Sec-Fetch başlıkları, 90027 çerez gevşekliği, 10094 Base64
(antiforgery token).

Tekrar tarama (düzeltmeden sonra, run 34902756773, baseline + full): Medium+ = 0 · Low = 1 (90004 COEP, KABUL) · 90022 yok ·
web logunda 5xx/istisna yok. Kalıcı haftalık tarama: CI `zap-scan` job'u (baseline `-a -j`), Medium+ bulguda kırmızı.
