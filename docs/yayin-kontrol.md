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
