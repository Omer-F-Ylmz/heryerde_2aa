# AV-1 — HerYerde tam tarama (10 Eylül 2026)

Kapsam: `HerYerde.*` + `tests` + `.github` + `appsettings*.json` + `wwwroot/js/site.js`.
Ortam: `http` profili, `HerYerdeVitrin` LocalDB, EF komut logu açık. Kod değiştirilmedi, commit yok.
Biçim: `ID | şiddet | dosya:satır | bulgu | öneri` — şiddet K/Y/O/D, KORUNUYOR olanlarda `-`.

## A — Güvenlik (20 başlık)

| ID | Durum | Şiddet | Kanıt | Bulgu / Öneri |
|----|-------|--------|-------|----------------|
| S01 | KORUNUYOR | - | EfEntityRepositoryBase.cs:19-34 | Ham SQL yok, tümü LINQ+parametre; `?ara=' OR '1'='1` → 200, hatasız. Öneri: yok |
| S02 | KORUNUYOR | - | tüm .cshtml (`Html.Raw` 0 kullanım) | XSS yükü adı `&lt;script&gt;` olarak yankılandı. Öneri: `Html.Raw` yasağını sürdür |
| S03 | KORUNUYOR | - | 20 `[HttpPost]` / 20 `[ValidateAntiForgeryToken]` | Tokensiz POST → 400. Öneri: global `AutoValidateAntiforgeryToken` ile unutma riskini kapat |
| S04 | KORUNUYOR | - | CartManager.cs:155, CheckoutController.cs:89 | Sepet satırı `cartId` ile kapsanmış, başka sepetin satırı silinemedi; sipariş `access_token`'sız 404. Öneri: yok |
| S05 | KISMİ | O | AdminAuthManager.cs:13-14,42-46 | 5 hata → 15 dk kilit çalışıyor; IP başına sınır yok, saldırgan admini kasten kilitleyip DoS yapabilir. Öneri: IP bazlı hız sınırı |
| S06 | KISMİ | Y | Program.cs:42-50, CartCookie.cs:17 | `HttpOnly`+`Lax` var; `Secure` politikası SameAsRequest ve `ForwardedHeaders` yok → TLS'i sonlandıran proxy arkasında çerezler Secure'suz gider. Öneri: `UseForwardedHeaders` + `CookieSecurePolicy.Always` |
| S07 | AÇIK | Y | Program.cs:72-77 (başlık middleware'i yok) | Yanıtta yalnız `Content-Type/Date/Server`; CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy yok. Öneri: tek bir başlık middleware'i ekle |
| S08 | KORUNUYOR | - | .gitignore:5, `git ls-files` | `appsettings.Development.json` izlenmiyor, prod sırları env'den. Öneri: yok |
| S09 | KORUNUYOR | - | CartController.cs:62, AuthController.cs:55 | `Url.IsLocalUrl`; `https://evil.com`, `//evil.com`, `/\evil.com`, `/%09/evil.com` hepsi `/sepet`'e döndü. Öneri: yok |
| S10 | KORUNUYOR | - | ProductManager.cs:97-107 | Güncelleme açık alan listesiyle; `CreatedAt/DeletedAt/Slug` forma bağlı değil. Öneri: yok |
| S11 | KORUNUYOR | - | CartManager.cs:136, OrderManager.cs:91 | Fiyat daima sunucudan, formdan fiyat alınmıyor (adet sınırı için bkz. B05). Öneri: yok |
| S12 | AÇIK | Y | OrderManager.cs:204-210, :74-82/:139-142 | `order_no` ve stok düşümü yarışa açık; bkz. B01/B02. Öneri: SEQUENCE + koşullu UPDATE |
| S13 | AÇIK | Y | Program.cs (RateLimiter yok), CartManager.cs:48 | Hız sınırı yok; 20 anonim `POST /sepet/ekle` → 20 `cart` satırı, temizleme işi de yok. Öneri: `AddRateLimiter` + sepet TTL işi |
| S14 | KISMİ | O | appsettings.json:16 | `AllowedHosts: "*"`; `Host: evil.example.com` → 200. Öneri: prod'da gerçek alan adını yaz |
| S15 | KORUNUYOR | - | ProductsController.cs:158-174 | Dosya yükleme yok (görsel yalnız URL metni); `/css/../appsettings.json` vb. 404. Öneri: görsel URL'inde `http/https` beyaz listesi |
| S16 | KISMİ | D | AdminAuthManager.cs:29-37 | Mesaj tek biçim ama zamanlama ayırt ediyor (bilinmeyen 2,5 ms / bilinen 62 ms) ve kilit mesajı hesabı ele veriyor. Öneri: bilinmeyen e-postada da sahte hash doğrula |
| S17 | KORUNUYOR | - | tüm mutasyon action'ları `[HttpPost]` | `/admin/products/delete/1`, `/sepet/sil?itemId=1` GET → 405. Öneri: yok |
| S18 | KORUNUYOR | - | `dotnet list package --vulnerable --include-transitive`, `npm audit` | İki tarama da temiz (0 bulgu). Öneri: CI'ya periyodik tarama ekle |
| S19 | KORUNUYOR | - | Program.cs:57-61, HomeController.cs:8 | Prod'da `UseExceptionHandler` → `Problem(500)`, yığın izi yok; Development'ta izin görünmesi beklenen. Öneri: yok |
| S20 | KORUNUYOR | - | app.log (`Parameters=[@p0='?']`) | `EnableSensitiveDataLogging` kapalı; telefon/adres/ad logda yok. Öneri: yönetici eylemleri için denetim izi (bkz. G07) |

## B — Bug / işlev

| ID | Şiddet | Dosya:satır | Bulgu | Öneri |
|----|--------|-------------|-------|-------|
| B01 | Y | OrderManager.cs:204-210 | 6 eşzamanlı ödeme: 1×302, 3×409, **2×500** — log `Cannot insert duplicate key ... ux_order_order_no (HY-20260910-0006)`; müşteri siparişini kaybediyor | Sıra numarasını SEQUENCE'tan al ya da çakışmada bir kez yeniden dene |
| B02 | Y | OrderManager.cs:74-82 ↔ :139-142 | Stok kontrolü işlemin dışında, düşüm içinde ve EF mutlak değer yazıyor (`stock = 3`, `stock - 2` değil) → kayıp güncelleme / aşırı satış | `rowversion` damgası veya koşullu `UPDATE ... WHERE stock >= @qty` |
| B03 | Y | OrderManager.cs:174-191 | Sipariş iptalinde stok iade edilmiyor: stok 3 → 2 adetlik sipariş → 1 → iptal → **hâlâ 1** | `IptalEdildi` geçişinde `order_item` üzerinden varyant stokunu geri ekle |
| B04 | Y | ProductManager.cs:308-311 + HerYerdeContext.cs:58 | Soft-delete edilen ürünün slug'ı sorgu süzgeci yüzünden görünmüyor; aynı adla ürün ekleme **500** (`ux_product_slug`) | Benzersizlik kontrolünü `IgnoreQueryFilters()` ile yap |
| B05 | Y | CartController.cs:34, CartManager.cs:101-146 | Adet üst sınırı yok: `quantity=999999` kabul edildi, 1.890.001.969,90 ₺'lik `HY-20260910-0002` siparişi oluştu | Satır başına makul üst sınır (ör. 10) ve stok kadar sınırla |
| B06 | O | CartManager.cs:113-122 | Stok 0 varyant sepete eklenebiliyor (engel yalnız site.js:54'te); hata ancak ödeme adımında 409 çıkıyor | `AddAsync` içinde stok kontrolü |
| B07 | O | CartManager.cs:136 | Sepetteki fiyat 30 gün donuyor: kampanya bittikten sonra ürün 2.490 ₺'den fiyatlandı (`HY-20260910-0003`, liste 3.190 ₺) | Ödeme anında fiyatı yeniden değerle, değiştiyse kullanıcıya göster |
| B08 | O | Store/Product.cshtml:33 + StoreController.cs:99 | Giyim ürününde kırıntı "Ev › Giyim" ve `/ev/giyim` bağlantısı **404** veriyor | Kırıntının kökünü ürünün kök alanından üret |
| B09 | D | CartController.cs:34,49 | `quantity=0` ve `-5` sessizce 1'e yuvarlanıp sepete ekleniyor, hata dönmüyor | `< 1` için 400 döndür |

Sorunsuz doğrulanan akışlar: Ev sepet→sipariş (kapıda `HY-20260910-0001`, havale `HY-20260910-0008`) ·
Giyim varyantlı sipariş + stok düşümü (3→1) · stok 0 varyantta ödeme 409 · süresi dolmuş kampanyada tam
fiyat (1.990 ₺) · boş/geçersiz form 400 + Türkçe mesaj · geçersiz telefon 400 · `PaymentMethod=3` 400 ·
sahte ve başka ürünün `variantId`'si reddedildi · `sayfa=0/-5/99999`, `sirala=xyz` kırılmıyor · admin
yetkisiz erişim → giriş sayfasına 302 · durum geçişleri (aşama atlama, geri dönüş, geçersiz enum: hepsi 400) · kilit 5. hatada.

## C — Performans

| ID | Şiddet | Dosya:satır | Bulgu | Öneri |
|----|--------|-------------|-------|-------|
| P01 | Y | ProductsController.cs:30-35 | `/admin/products` N+1: ürün başına `GetVariantsAsync` → 18 üründe **21 sorgu** (diğer sayfalar 2-6) | Stok toplamını tek `GroupBy` sorgusunda al |
| P02 | O | StoreController.cs:65,89 | `/ev` ve `/urun` tüm `product` tablosunu belleğe alıp orada süzüp sayfalıyor | Filtre/sıralama/sayfalamayı SQL'e taşı |
| P03 | O | Program.cs (sıkıştırma yok) | Yanıt sıkıştırma kapalı; Lighthouse `uses-text-compression` **45 KiB** tasarruf diyor | `UseResponseCompression` (br + gzip) |
| P04 | O | Program.cs:74 | Statik dosyalarda `Cache-Control` yok (yalnız ETag/Last-Modified) | `asp-append-version` zaten var; `max-age=31536000, immutable` ver |
| P05 | D | HerYerdeContext.cs:124-146 | `order` tablosunda `status`/`created_at` indeksi yok; yönetim listesi tam tarama | `(status, created_at DESC)` indeksi |
| P06 | D | _Layout.cshtml:4 | Sepet rozeti için her sayfada ek sorgu | Sepet çerezi yokken sorguyu atla |
| P07 | KORUNUYOR | - | EfEntityRepositoryBase.cs:20,27 | Okuma yollarında `AsNoTracking` uygulanmış; eksik yok |

Sorgu / süre (tek istek, sepette 2 satır): `/` 3 sorgu 3,9 ms · `/ev` 3 / 3,4 ms · `/ev/tencere-tava` 3 / 3,1 ms ·
`/urun/{slug}` 6 / 3,9 ms · `/sepet` 5 / 4,6 ms · `/odeme` 5 / 3,5 ms · `/admin/products` **21** / 6,5 ms · `/admin/orders` 1 / 2,7 ms.
Lighthouse (mobil, 390 px, performans): `/` **93** · `/ev` **92** · `/urun/granit-dokum-tencere-seti` **92** —
FCP/LCP 2,6-2,7 s, TBT 0 ms, CLS 0; tek fırsat metin sıkıştırma (P03).

## D — Eksikler

| ID | Var/Yok | Konu | Kapsam |
|----|---------|------|--------|
| G01 | YOK | SEO meta: sayfa başına `description` yok, `_Layout.cshtml:12` tek sabit metin | S |
| G02 | YOK | OG / Twitter kartı / `canonical` — hiçbir görünümde yok | S |
| G03 | YOK | `sitemap.xml` ve `robots.txt` (ikisi de 404) | S |
| G04 | YOK | JSON-LD (`Product`, `BreadcrumbList`) | S |
| G05 | KISMİ | 404 markalı (`Home/NotFound.cshtml`), 500 ise çıplak `Problem` JSON'u | S |
| G06 | VAR | Health check — `Program.cs:35,79`, `/health` → 200 "Healthy" | — |
| G07 | YOK | Serilog / yapılandırılmış log / yönetici denetim izi | M |
| G08 | YOK | Ev ürünlerinde stok ve "tükendi": varyantsız ürün stok tutmuyor, sınırsız sipariş edilebiliyor | M |
| G09 | YOK | "1 alana 1 hediye" sipariş mekaniği (`BadgeKind.Gift` yalnız styleguide'da) | M |
| G10 | YOK | Arama — `_Layout.cshtml:27` kutusu `readonly`, sunucu tarafı yok | M |
| G11 | YOK | Fiyat filtresi (`sirala=fiyat` var, aralık süzgeci yok) | S |
| G12 | YOK | Yönetici parola değiştirme ekranı | S |
| G13 | YOK | Terk edilmiş sepetleri temizleyen arka plan işi | S |
| G14 | YOK | `docker-compose.prod.yml`'de `Admin__Email`/`Admin__Password` yok → prod'da yönetici tohumlanmaz | S |
| G15 | YOK | Prod açılışında migration uygulama adımı (`Program.cs` `Migrate()` çağırmıyor) | S |
