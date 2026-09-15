# ZAP-3 — OWASP ZAP otomatik tarama, D10-D12 sonrası (15 Eylül 2026)

Ortam: ZAP-2 ile aynı (`docker-compose.prod.yml` + `docker-compose.zap.yml`, Production, compose SQL Server, `AllowedHosts=localhost`,
GitHub Actions ubuntu, geçici `zap-tarama-3` dalı, iş akışı `.github/workflows/zap-3.yml`). Farklar:
- Tohum: `tools/ci-ornek-veri.sh` (katalog) + `.zap/ornek-siparis.sh` — sepetten havale siparişi açılır, yönetimden PDF fatura yüklenir;
  teşekkür sayfasının anahtarlı yolu maskelenmiş olarak taramaya verilir.
- Vitrin: `zap-baseline.py -a -j` (sepet çerezi replacer) + ayrı sayfa baseline'ları: teşekkür sayfası (havale bildirim formu
  `/siparis/{no}/odeme-bildir`, fatura indirme `/siparis/{no}/fatura`) ve `/siparis-sorgula`; `zap-full-scan.py -n vitrin.context -j -m 5`
  (`/admin.*` dışarıda, `rules.csrf.ignore.attname=data-no-csrf`).
- Yönetim (yeni): 2FA'sız CI tarama hesabı (`HERYERDE_ADMIN_EMAIL`, CI'ya özgü sahte parola) `.zap/yonetim-cerezi.sh` ile giriş yapar;
  çerez `zap.sh -cmd -config replacer…` ile her isteğe eklenir, `.zap/yonetim.yaml` Automation Framework planı: örümcek
  `/admin/products`'tan, pasif bekleme, aktif tarama (40 dk tavan). Oturumu/hesabı bozan uçlar dışarıda: çıkış, parola, oturumları kapat,
  iki adım, silme, anonimleştirme, iade. Oturumlu tarandığının kanıtı adımda denetlenir (web logunda `GET /admin/rapor responded 200`).
- Yeni rotalar: `/siparis-sorgula`, `/admin/orders/new`, `/admin/rapor` (+ CSV), `/admin/products/export|import`, `/admin/orders/export`,
  `/odeme-bildir`, fatura indirme (vitrin ve yönetim).
- Akış düzeltmeleri (tarama geçerliliği): baseline/full betikleri hedefi köke kırptığı için `/admin` bağlamıyla başlamıyordu → AF planı;
  `zap.sh` varsayılan 8080 portu uygulamayla çakışıyordu → `-port 8090`; AF `replacer` işi çerezi eklemedi (ilk koşu oturumsuz, yalnız giriş
  sayfası tarandı) → `-config replacer`; rapor dosyası yoksa özet kırmızı; çerezler loglarda maskeli.

Taramalar (Informational listelenmez):

Koşu | kapsam | sonuç
---|---|---
34984981149 | vitrin + yönetim (baseline betiği) | vitrin tamam, Medium+ 0; yönetim başlamadı (hedef kırpma)
34988371655 | yalnız yönetim (AF, replacer işi) | geçersiz — oturumsuz, yalnız /admin/auth/*
34990002268 | yalnız yönetim (oturumlu, eski kod) | 28 dk, 22 yönetim yolu 200; High(Low) 6 Path Traversal; 1380 × 500 (onay kutusu)
34994622175 | vitrin + yönetim (e55f429, düzeltmeli) | vitrin Medium+ 0, 5xx 0; yönetim 40 dk (tavan), 23 yolu 200, oturum sonuna kadar; 5xx 22; High(Low) 6 yanlış pozitif
35001191519 | yalnız yönetim (alertFilter) | filtre uygulandı (6 → False Positive, 0 örnek); yeni High(Low) 40018 = eşzamanlı slug 500'ü (9 × 500); özet kapısı FP satırını da sayıyordu
35005969698 | vitrin + yönetim (9bd4c4d: slug 409, özet kapısı FP satırını saymaz) | YEŞİL — Medium+ 0 (6 Path Traversal `High (False Positive)`, 0 örnek); Low: 90004 COEP, 2 Private IP, 90022 (yalnız eşzamanlı okundu/onayla); vitrin 5xx 0; yönetim 40 dk (tavan), oturum sonuna kadar (`/admin/rapor` 200 × 505); 5xx 18, hepsi DbUpdateConcurrencyException (mesajlar 7, yorumlar 11); FormatException 0, işlenmemiş `ux_category_slug` 0

ID | risk | uyarı adı | URL | karar
---|---|---|---|---
90004 | Low | Cross-Origin-Embedder-Policy Header Missing | http://localhost:8080/ | KABUL — ZAP-1/2 ile aynı gerekçe (`require-corp` dış ürün görsellerini kırar)
90022 | Low | Application Error Disclosure | POST /admin/categories/create, /admin/products/create, /admin/orders/new | DÜZELTİLDİ — onay kutusuna bool olmayan değer form yeniden çizilirken FormatException (500) veriyordu; `CheckboxBoolModelBinder` (CheckboxBindingTests). Son koşuda FormatException 0
90022 | Low | Application Error Disclosure | POST /admin/mesajlar/{id}/okundu, /admin/yorumlar/{id}/onayla (son koşu 18) | KABUL — tarayıcı aynı saniyede kaydı silip güncelleyince DbUpdateConcurrencyException; veri bozulmaz, yanıtta ayrıntı yok, tek yöneticili panelde gerçekçi değil
40018 + 90022 | High (güven Low) + Low | SQL Injection / Application Error Disclosure | POST /admin/categories/create (`Name=ZAP'(`), /admin/categories/edit/2 | DÜZELTİLDİ (enjeksiyon değil) — kanıt yalnız 500 durum satırı; web logunda `ux_category_slug` DbUpdateException: aynı adla eşzamanlı isteklerde slug denetimi ikisinde de boş görüp ikinci kayıt indekse çarpıyordu, sorgular parametreli. Kayıt indeks çakışmasında 409 döner (CategorySlugRaceTests)
2 | Low | Private IP Disclosure | GET /admin/denetim, /admin/mesajlar, /admin/yorumlar | KABUL — denetim izi işlemi yapanın IP'sini yöneticiye gösterir (tasarım); mesaj/yorumdaki `172.18.0.1:44717` sabit portlu ZAP geri çağırma adresi olarak saldırı yüküyle metne girmiş, bu sayfalar IP alanı göstermez
6 | High (güven Low) | Path Traversal (6-5) | POST /admin/categories/edit/1, /edit/2 — `Id`, `SortOrder` | YANLIŞ POZİTİF — sezgi URL'nin son parçasını (`1`/`2`) parametreye koyup durum farkına bakar; kanıt boş, içerik imzası kuralları (6-1..6-4) tetiklenmedi; parametreler `int`, EF güncellemesine gider, dosya yoluna ulaşmaz (tek dosya işlemi `SaveCategoryAsync(int id)`); 200/302/400 dalgalanması formun kendi kuralı. Planda yalnız kural 6 + bu URL + bu iki parametre için `alertFilter` ile işaretlendi

Informational (karar gerekmez): 10111/10112 giriş ve oturum isteği tanıma, 10031 kullanıcı kontrollü öznitelik (`?ara=`, `?durum=`,
`?baslangic=` — Razor kaçışlı), 10104 UA fuzzer, 10049/10050 önbellek, 10058 GET for POST (`/siparis-sorgula`), 20012 `/iletisim` Anti-CSRF
(formda token var), 90027 çerez gevşekliği, 10094 Base64 (antiforgery token), 90005 Sec-Fetch başlıkları.

Kapsam notları: teşekkür sayfası, havale bildirimi ve fatura indirme yalnız sayfa baseline'ında (anahtar yalnız o adıma verilir; full
taramada `/siparis/{no}` anahtarsız 404). Yönetim aktif taraması 40 dk tavanında durdu. Kalıcı haftalık tarama: CI `zap-scan` job'u
(vitrin baseline `-a -j`), Medium+ bulguda kırmızı; elle tetiklenen koşu yeşil.
