# ZAP-1 — OWASP ZAP otomatik tarama (11 Eylül 2026)

Ortam: `docker-compose.prod.yml` + `docker-compose.zap.yml` (Production, compose SQL Server, `AllowedHosts=localhost`,
`Shop__BaseUrl=http://localhost:8080`, tarama için yükseltilmiş hız sınırı), GitHub Actions ubuntu koşucusu.
- Baseline: `zap-baseline.py -t http://localhost:8080 -a -j` (+ sepet çerezi, `.zap/sepet-cerezi.sh` → replacer `Cookie`).
- Full: `zap-full-scan.py -t http://localhost:8080 -n vitrin.context -j -m 5` (+ sepet çerezi; `.zap/vitrin.context` /admin'i dışlar;
  `-config rules.csrf.ignore.attname=data-no-csrf`).
- Haftalık: CI `zap-scan` job'u (baseline `-a -j`), Medium+ bulguda kırmızı, rapor `zap-report` artifact'ı.

Bulgular (ilk tarama; Informational listelenmez):

ID | risk | uyarı adı | URL | karar
---|---|---|---|---
10055 | Medium | CSP: Wildcard Directive (img-src `https:`) | http://localhost:8080/ (tüm sayfalar) | DÜZELT — img-src `'self' data:` + `Shop:ImageOrigins` (varsayılan `https://placehold.co`)
10038 | Medium | Content Security Policy (CSP) Header Not Set | POST http://localhost:8080/sepet/guncelle (500) | DÜZELT — başlıklar `OnStarting` ile yazılır; hata sayfası da taşır
90003 | Medium | Sub Resource Integrity Attribute Missing | http://localhost:8080/admin/auth/login | DÜZELT — CSP'nin zaten engellediği Google Fonts bağlantısı kaldırıldı (yazı tipi site.css'ten)
20012 | Medium | Anti-CSRF Tokens Check (arama formu) | POST http://localhost:8080/odeme | DÜZELT — yanlış pozitif: token'sız formlar yalnız GET arama/filtre, `data-no-csrf` ile işaretli; tarama bu işareti Informational sayar
90022 | Low | Application Error Disclosure | POST http://localhost:8080/sepet/guncelle | DÜZELT — eş zamanlı azalt+sil `DbUpdateConcurrencyException` → 404 "Sepet satırı bulunamadı"
10063 | Low | Permissions Policy Header Not Set | POST http://localhost:8080/sepet/guncelle | DÜZELT — 10038 ile aynı kök neden
90004 | Low | Cross-Origin-Opener-Policy Header Missing | http://localhost:8080/ | DÜZELT — `same-origin`
90004 | Low | Cross-Origin-Resource-Policy Header Missing | http://localhost:8080/ | DÜZELT — `same-origin`
90004 | Low | Cross-Origin-Embedder-Policy Header Missing | http://localhost:8080/ | KABUL — `require-corp` CORP göndermeyen dış ürün görsellerini (placehold.co) kırar; paylaşılan bellek/SharedArrayBuffer kullanılmıyor

Tekrar tarama (düzeltmelerden sonra, baseline + full): Medium+ = 0 · Low = 1 (90004 COEP, KABUL) ·
20012 yalnız Informational (`data-no-csrf` işaretli arama formu) · web logunda 5xx/istisna yok.
