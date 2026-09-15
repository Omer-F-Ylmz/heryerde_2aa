# Dış hesaplar

Yayın öncesi dışarıdan alınıp yapılandırmaya yazılması gereken değerler. Gizli olanlar
`appsettings*.json` yerine ortam değişkenine yazılır (`:` yerine `__`). Tümü boşken uygulama açılır
ve testler geçer; eksik ayarın tek sonucu o özelliğin sessizce devre dışı kalmasıdır (bildirim
gönderimi "atlandı" olarak uyarı seviyesinde loglanır).

Prod'da değerler `docker-compose.prod.yml` üzerinden verilir; son sütun oradaki değişkendir.
"Evet" satırlarının değişkeni compose'da `:?` ile zorunludur: biri boşsa `docker compose up` hiç başlamaz.

| Değer | Nereden alınır | Anahtar / ortam değişkeni | Yayın öncesi zorunlu mu | Compose değişkeni |
|---|---|---|---|---|
| Veritabanı bağlantısı | Sunucu sağlayıcısı | `ConnectionStrings__Default` (env) | Evet | `HERYERDE_CONNECTION_STRING` |
| SQL Server sa parolası (compose içindeki veritabanı) | [Ömer] üretir | `MSSQL_SA_PASSWORD` (db servisi) | Evet | `HERYERDE_DB_PASSWORD` |
| Yönetici e-postası ve parolası | [MÜŞTERİ] | `Admin__Email`, `Admin__Password` (env) | Evet | `HERYERDE_ADMIN_EMAIL`, `HERYERDE_ADMIN_PASSWORD` |
| Yayın adresi (canonical, e-posta bağlantıları) | Alan adı kaydı | `Shop:BaseUrl` | Evet | `HERYERDE_BASE_URL` |
| İzinli alan adları (Host süzgeci) | Alan adı kaydı | `AllowedHosts` (ör. `alanadi.com;www.alanadi.com`) | Evet | `HERYERDE_ALLOWED_HOSTS` |
| SMTP sunucusu | E-posta/hosting sağlayıcısı | `Notifications:Host` | Evet | `HERYERDE_SMTP_HOST` |
| SMTP portu | E-posta sağlayıcısı | `Notifications:Port` | Hayır (varsayılan 587) | `HERYERDE_SMTP_PORT` |
| SMTP kullanıcı adı | E-posta sağlayıcısı | `Notifications__User` (env) | Evet | `HERYERDE_SMTP_USER` |
| SMTP parolası | E-posta sağlayıcısı | `Notifications__Password` (env) | Evet | `HERYERDE_SMTP_PASSWORD` |
| Gönderen e-posta adresi | [MÜŞTERİ] (mağaza alan adı) | `Notifications__From` (env) | Evet | `HERYERDE_SMTP_FROM` |
| "Yeni sipariş" bildiriminin gideceği adres | [MÜŞTERİ] | `Notifications__StoreTo` (env) | Evet | `HERYERDE_STORE_EMAIL` |
| Kargo firmaları ve takip adresi şablonu | [MÜŞTERİ] kargo anlaşması | `Shipping:Carriers` (`Name`, `TrackingUrl`; `{0}` takip numarası) | Evet | — (appsettings) |
| Bedava kargo eşiği | [MÜŞTERİ] | `Shop:FreeShippingOver` (0 = eşik kapalı) | Evet (şu an geçici 2500) | — (appsettings) |
| Kargo ücreti | [MÜŞTERİ] kargo anlaşması | `Shop:ShippingFee` | Evet | — (appsettings) |
| "Son N adet" rozeti ve yönetim düşük stok eşiği | [MÜŞTERİ] | `Shop:LowStockBadgeAt` (vitrin, varsayılan 3), `Shop:LowStockAlertAt` (yönetim, varsayılan 5) | Hayır | — (appsettings) |
| Havale/EFT IBAN | [MÜŞTERİ] bankası | `Shop__Iban` (env) | Evet | `HERYERDE_IBAN` |
| WhatsApp sipariş numarası | [MÜŞTERİ] | `Shop:WhatsApp` | Evet | — (appsettings) |
| İyzico API anahtarı | [MÜŞTERİ] İyzico üye iş yeri paneli (sandbox: sandbox-merchant.iyzipay.com) | `Iyzico__ApiKey` (env) | Evet | `HERYERDE_IYZICO_API_KEY` |
| İyzico gizli anahtarı | [MÜŞTERİ] İyzico üye iş yeri paneli | `Iyzico__SecretKey` (env) | Evet | `HERYERDE_IYZICO_SECRET_KEY` |
| İyzico API adresi | İyzico | `Iyzico:BaseUrl` (varsayılan `https://sandbox-api.iyzipay.com`; canlı `https://api.iyzipay.com`) | Evet | `HERYERDE_IYZICO_BASE_URL` |
| 3D doğrulama formunun gideceği köken | İyzico / banka | `Iyzico:CspSources` (CSP `form-action`, `frame-src`; yalnız `/odeme*`) | Evet | `HERYERDE_IYZICO_CSP_SOURCE` (ilk köken) |
| Hata izleme DSN'i | [Ömer] Sentry projesi (Settings › Client Keys) | `Sentry__Dsn` (env) | Evet | `HERYERDE_SENTRY_DSN` |
| ETBİS kayıt numarası | [MÜŞTERİ] eticaret.gov.tr ETBİS kaydı | `Legal:EtbisNo` (boşken altbilgide bant görünmez) | Hayır (kayıt tamamlanınca) | `HERYERDE_ETBIS_NO` |

## Notlar
- `Notifications:Host` ya da `Notifications__From` boşken gönderici "yapılandırılmamış" sayılır;
  kuyruktaki postalar silinmez, ayar geldiğinde ilk turda gönderilir. Bekleyen en eski posta 10 dakikayı
  geçerse `/health/ready` 503 döner.
- `Notifications__StoreTo` boşsa mağaza bildirimi hiç kuyruğa girmez; müşteri postası etkilenmez.
- `Iyzico__ApiKey` ya da `Iyzico__SecretKey` boşken ödeme sayfasında kart seçeneği hiç görünmez, kartla
  gelen sipariş isteği 400 alır; uygulama açılır. Canlı anahtarlar gelince `Iyzico:BaseUrl` ve
  `Iyzico:CspSources` canlı adrese çevrilir; bankanın 3DS sayfası farklı kökene yönlenirse o da listeye eklenir
  (compose'da `Iyzico__CspSources__1` satırı eklenir).
- `Sentry__Dsn` boşken hata izleme tümüyle kapalıdır. Doluyken yalnız Error ve üstü olaylar gider; kişisel veri
  gönderimi kapalı, telefon/e-posta/adres olay çıkmadan maskelenir.
- `Shipping:Carriers` listesinde olmayan bir kargo firmasıyla "Kargoda" durumuna geçilemez (400).
- Takip adresi şablonu boş bırakılırsa teşekkür sayfasında bağlantı değil yalnız takip numarası çıkar.
