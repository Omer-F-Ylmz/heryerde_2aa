# Yayın kontrol listesi

Barındırma kararı verildikten sonra sırayla yürütülür. Her satırın bir sahibi vardır:
**[Ömer]** altyapı ve hesaplar, **[MÜŞTERİ]** mağaza sahibinden gelen bilgi/karar, **[CC]** Claude Code'un depo içinde yaptığı iş.
Değerlerin tam listesi docs/dis-hesaplar.md, yedek ayrıntısı docs/yedekleme.md.

| # | Adım | Doğrulama | Sahip |
|---|---|---|---|
| 1 | Alan adını kaydet / müşteriden devral | Kayıt panelinde alan adı müşteri adına | [MÜŞTERİ] |
| 2 | DNS: A/AAAA (ve `www` CNAME) sunucuya | `dig +short alanadi.com` sunucu IP'si | [Ömer] |
| 3 | TLS: vekil (Caddy/nginx/sağlayıcı) sertifikası, 80→443 yönlendirme | `curl -sI https://alanadi.com` 200, sertifika geçerli | [Ömer] |
| 4 | Vekil adresini `ForwardedHeaders:KnownProxies`/`KnownNetworks`'e yaz | Hız sınırı gerçek istemci IP'siyle çalışır (loglarda vekil IP'si değil) | [CC] |
| 5 | Sunucuda `.env`: docs/dis-hesaplar.md'deki tüm `HERYERDE_*` değerleri | `docker compose -f docker-compose.prod.yml config` hatasız | [Ömer] |
| 6 | Müşteriden gelen değerler: IBAN, WhatsApp, kargo firmaları, ücret/eşik, yönetici e-postası, bildirim adresi | docs/dis-hesaplar.md satırları dolu | [MÜŞTERİ] |
| 7 | appsettings'teki müşteri değerlerini (WhatsApp, kargo, ücret/eşik) güncelle | İlgili testler yeşil, ödeme sayfasında doğru tutar | [CC] |
| 8 | SMTP hesabı ve gönderen alan adı (SPF/DKIM) | Test siparişinin postası gelen kutusuna düşer | [Ömer] |
| 9 | Sentry projesi, DSN `.env`'e | Bilerek tetiklenen hata Sentry'de, telefon/e-posta maskeli | [Ömer] |
| 10 | Yedek klasörü izinleri, `docker compose up -d` (migration açılışta uygulanır) | `/health/ready` 200; `docker compose ps` web healthy | [Ömer] |
| 11 | İlk yönetici: `Admin__Email`/`Admin__Password` ile açılış, ilk girişte parola değişimi | `/admin` girişi, denetim kaydında giriş satırı | [MÜŞTERİ] |
| 12 | Ürün ithali: ham fotoğraflar sunucuya, `docker compose run --rm -v <ham>:/app/brand_assets/raw:ro web --ithal brand_assets/raw` | "İthal: N ürün" çıktısı; ürünler taslak | [Ömer] |
| 13 | İthal edilen ürünlerin fiyat/stok/açıklamasını gir, yayına al | Vitrinde ürünler, 1 ₺ "fiyat eksik" bayrağı kalmadı | [MÜŞTERİ] |
| 14 | İyzico canlı üye iş yeri başvurusu, canlı anahtarlar | İyzico panelinde hesap onaylı | [MÜŞTERİ] |
| 15 | Canlı anahtarlar ve `HERYERDE_IYZICO_BASE_URL=https://api.iyzipay.com`, CSP kökeni | Ödeme sayfasında kart seçeneği; CSP `form-action` canlı köken | [Ömer] |
| 16 | Canlı kartla küçük tutarlı gerçek 3DS ödeme + iade | Sipariş "ödendi", banka 3DS sayfası açıldı, iade İyzico panelinde | [MÜŞTERİ] |
| 17 | Bankanın 3DS sayfası farklı kökene yönlendiyse CSP'ye ekle | 3DS formu tarayıcı konsolunda CSP hatasız | [CC] |
| 18 | ETBİS kaydı (eticaret.gov.tr) | Kayıt numarası alındı | [MÜŞTERİ] |
| 19 | `HERYERDE_ETBIS_NO` `.env`'e, `web` yeniden başlat | Altbilgide ETBİS bandı ve numara | [Ömer] |
| 20 | Yasal metinlerdeki satıcı bilgileri (unvan, adres, MERSİS/vergi no) | docs/yasal-notlar.md eksikleri kapandı, sayfalarda gerçek bilgi | [MÜŞTERİ] |
| 21 | Satıcı bilgilerini yasal şablonlara işle, `LegalDocs.Version` ilerlet | LegalPagesTests yeşil | [CC] |
| 22 | İlk elle yedek, provaya aç (docs/yedekleme.md) | "Geri yükleme: HerYerde_prova, N ürün" ve prova `/health/ready` 200 | [Ömer] |
| 23 | Harici yedek hedefini seç, aktarımı kur, docs/yedekleme.md'ye yaz | Uzak depoda dünkü `.bak` ve arşiv | [Ömer] |
| 24 | Uptime izleme: `https://alanadi.com/health/ready` (1-5 dk, 2 ardışık hata alarmı) | Test alarmı e-posta/telefona düştü | [Ömer] |
| 25 | Canlı adrese ZAP baseline (docs/zap-2.md komutu, hedef canlı adres) | Medium+ bulgu yok | [CC] |
| 26 | Smoke: Actions › CI › Run workflow, `base_url=https://alanadi.com` | `smoke` job'u yeşil (HSTS dahil) | [CC] |
| 27 | Google Search Console: alan adı doğrulama, `sitemap.xml` gönderimi | Site haritası "Başarılı" | [MÜŞTERİ] |
| 28 | Yayın duyurusu (Instagram profil bağlantısı) | Profil bağlantısı alan adına gidiyor | [MÜŞTERİ] |
| 29 | Ayda bir: son canlı yedeği provaya aç | Prova çıktısı ve tarih bu dosyanın altına not | [Ömer] |

## Sürekli yayın (CD) ve staging

`.github/workflows/deploy.yml`. Staging, CI main'e push'ta yeşil bitince **otomatik**; prod yalnız **elle**
(Actions › Deploy › Run workflow, `environment=production`). Hedef repo değişkeni `DEPLOY_TARGET` ile seçilir; boşsa
iş "DEPLOY_TARGET tanımsız" notuyla atlanır. Staging ortamı (`ASPNETCORE_ENVIRONMENT=Staging`, `appsettings.Staging.json`):
her yanıtta `X-Robots-Tag: noindex, nofollow`, `robots.txt` her şeyi kapatır, Basic Auth kapısı (`STAGING_USER` /
`STAGING_PASS`; biri boşsa kapı kapalı kalır, `/health` açık), Sentry ortamı `staging`, İyzico sandbox, gerçek SMTP yok
(postalar outbox'ta "atlandı").

| Adım | Plesk / runasp.net (`DEPLOY_TARGET=plesk`) | Compose / SSH (`DEPLOY_TARGET=compose`) |
|---|---|---|
| GitHub ortamları | `staging` ve `production` ortamları; `production`'a koruma kuralı (onaylayıcı) [Ömer] | Aynı [Ömer] |
| Ortam değişkenleri (vars) | `SITE_URL`, `PLESK_SERVER`, `PLESK_SITE`, `DEPLOY_METHOD` (`msdeploy` varsayılan ya da `ftp`), FTP'de `FTP_SERVER`, `FTP_DIR` | `SITE_URL`, `SSH_HOST`, `SSH_USER`, `DEPLOY_PATH` |
| Ortam gizlileri (secrets) | `PLESK_USER`, `PLESK_PASSWORD` (FTP'de `FTP_USER`, `FTP_PASSWORD`); `APP_SETTINGS_JSON` = `{"ConnectionStrings__Default": "…", "Admin__Email": "…", "Iyzico__ApiKey": "…", …}` (docs/dis-hesaplar.md anahtarları, `:` yerine `__`); staging'de `STAGING_USER`, `STAGING_PASS` | `SSH_KEY` (yalnız yayın için anahtar), `SSH_KNOWN_HOSTS` (`ssh-keyscan` çıktısı, elle doğrulanmış); staging'de `STAGING_USER`, `STAGING_PASS`. Uygulama değerleri sunucudaki `.env`'de (docs/dis-hesaplar.md) ve staging'de `STAGING_USER`/`STAGING_PASS` da |
| Derleme | Runner'da `npm run css:build` + `dotnet publish -p:EnvironmentName=…`; `tools/deploy/plesk.ps1 -Action Configure` ayarları `web.config` `environmentVariables`'a yazar | Runner'da `docker build`, imaj `ghcr.io/<repo>/web:<commit>` |
| Migration (`--migrate`) | Runner'dan uzak veritabanına: `dotnet HerYerde.Web.dll --migrate`, dosyalardan önce | Sunucuda: `docker compose run --rm web --migrate` (yeni imajla), `up`'tan önce |
| Yayın | Web Deploy (`msdeploy`, AppOffline; `wwwroot/uploads`, `private`, `logs` korunur) ya da FTP (`app_offline.htm`, silme yok) | `.env`'e `HERYERDE_WEB_IMAGE=<imaj>`, `docker compose -f docker-compose.prod.yml [-f docker-compose.staging.yml] up -d --wait` |
| Smoke | `tools/smoke.ps1 -BaseUrl $SITE_URL` (staging'de `-Staging -Credential kullanıcı:parola`) | Aynı |
| Başarı | `yayin/<ortam>/<çalıştırma>` etiketi | `.env`'deki imaj etiketi yeni sürüm olarak kalır |
| Rollback | Migration sonrası bir adım kırmızıysa son `yayin/<ortam>/*` etiketi yeniden derlenip yayımlanır ve smoke koşar | `.env`'e önceki `HERYERDE_WEB_IMAGE` geri yazılır, compose o imajla kalkar, smoke koşar |
| Veritabanı | Geçişler geri alınmaz: eklemeli yazılır; bozuk veri gerekirse yedekten döner (docs/yedekleme.md) | Aynı |
| İlk kurulum | runasp.net panelinde site, SQL Server veritabanı ve Web Deploy/FTP kullanıcısı; staging için ayrı site ve veritabanı | Staging için ayrı sunucu ya da klasör; `DEPLOY_PATH`'te `.env`, `backups/` (docs/yedekleme.md) |
