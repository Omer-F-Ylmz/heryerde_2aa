# KAPANIŞ-3 — D10-D12 sonrası kapanış listesi (15 Eylül 2026)

Ortam: Development, ayrı LocalDB'ler (HerYerdeK3F/K3F2 fonksiyon, HerYerdeK3S güvenlik, HerYerdeVitrin görsel/Lighthouse), sahte
İyzico (yerel HTTP, gerçek imza), gerçek HTTP istekleri (node betikleri). ZAP: GitHub Actions, prod compose (bkz. `docs/zap-3.md`).
Şiddet: Y yüksek · O orta · D düşük. Satır biçimi: `ID | şiddet | dosya:satır/URL | bulgu | karar`.

## 1. Fonksiyon
Mevcut 576 test yeşil (başlangıç) → 600 yeşil (24 yeni kırmızı-önce test/vaka). Uçtan uca ilk koşu 187 kontrol / 182 geçti; düzeltmeden sonra
188/188 (yeni kontrol: teyitsiz WhatsApp siparişi 400): varyantlı Ev siparişi + iptal stok iadesi · Hazırlanıyor zinciri · sipariş sorgulama ·
manuel sipariş (kaynak, kargo ücreti ezme) · parola sıfırlama bağlantısı · 2FA kurulum/giriş/yedek kod/kapatma · fatura yükle-indir ·
havale bildirimi + onay · düzenleme (adet artı/eksi stok) · kart iadesi (sahte sağlayıcı) · Excel dışa aktar → düzenle → önizleme → uygula ·
sipariş CSV · rapor tutarlılığı: elle toplam 1579,70 (kapıda teslim 399,90 + havale onaylı 399,90 + kart başarılı 779,90; iptal/iade ve
bekleyen hariç) = rapor ciro 1579,70, ödenmiş sipariş 3, dönem satırları toplamı eşit.

ID | şiddet | dosya:satır/URL | bulgu | karar
---|---|---|---|---
F-01 | Y | HerYerde.Business/Concrete/ProductTransferManager.cs:126,158 | Adım: dışa aktar → satış (stok 23→21) → aynı dosyayı içe al. Beklenen: satılan adet korunur. Gözlenen: stok 23'e geri yazıldı | DÜZELTİLDİ — dışa aktarım çok gizli `disa_aktarim` sayfasına id/sku → stok yazar (`ProductSheet.cs:44`); hücre aktarılan değere eşitse stok yazılmaz, değiştirilmişse yazılır (ProductTransferTests.Eski_disa_aktarim_satistan_sonra_yuklenince_dokunulmamis_stok_ezilmez_degistirilen_yazilir; canlı 21 kaldı)
F-02 | Y | HerYerde.Business/Concrete/OrderManager.cs:416 | Adım: havalesi onaylanmış siparişte adet 1→3. Beklenen: 409. Gözlenen: toplam değişti, onaylı havale tutarıyla ayrıştı | DÜZELTİLDİ — `HavaleEft` ve durum ≠ Beklemede iken adet değişimi 409 (OrderBackOfficeTests.Onayli_havale_siparisinde_adet_degismez_409_toplam_ve_stok_ayni)
F-03 | O | HerYerde.Business/Concrete/AdminAuthManager.cs:85,304 | Adım: aynı TOTP kodu ikinci oturumda. Beklenen: red. Gözlenen: 30 sn penceresinde tekrar kabul | DÜZELTİLDİ — kabul edilen adım `admin_user.totp_last_step`'e yazılır, aynı/eski adım 400; migration `TotpLastStep` (AdminAccountTests.Kullanilmis_totp_kodu_ayni_pencerede_ikinci_giriste_gecmez)
F-04 | O | HerYerde.Web/Infrastructure/CsvFile.cs:31 | CSV'de `-` ile başlayan alan formül olarak açılıyordu (= S-01) | DÜZELTİLDİ — bkz. S-01
F-05 | D | HerYerde.Business/Concrete/OrderManager.cs:376 | Düzenlemede adet düşünce kargo ücreti yeniden hesaplanmaz (ücretsiz kargo eşiği) | KABUL — kargo ücreti sipariş anında müşteriyle kararlaştırılan tutardır; düzenleme yalnız kalem ve teslimat bilgisini değiştirir, ücret değişikliği yeni sipariş/iptalle yapılır
F-06 | D | HerYerde.Business/Concrete/ProductTransferManager.cs:278 | İçe aktarma önizlemesi değişmeyen satırı da "güncelle" sayar | KABUL — önizleme yazılacak satırları sayar, veri riski yok (F-01 sonrası stok da korunur); fark gösterimi ayrı iş
F-07 | D | HerYerde.Business/Concrete/ReportManager.cs:65 | Çok satanlarda ad/adet/tutar eşitken sıra kararsız | DÜZELTİLDİ — `ThenBy(Sku, Ordinal)` (SalesReportTests.Cok_satanlarda_ad_adet_tutar_esitse_stok_koduna_gore_sirali; canlı 3/3 aynı sıra)
F-08 | D | HerYerde.Business/Concrete/OrderManager.cs:292 | Stok yetersizliğiyle reddedilen manuel sipariş sipariş numarası harcıyor (HY-…-0007 boşluğu) | KABUL — sipariş numarası fatura seri numarası değildir, boşluk yasal/işlevsel sorun yaratmaz

## 2. Güvenlik
Canlı doğrulanan, bulgu çıkmayan yüzeyler: parola sıfırlama anahtarı 256 bit CSPRNG, SHA-256 özet, 30 dk, tek kullanım, başka hesabın
anahtarı geçmez, bağlantı `Shop:BaseUrl`'den (Host başlığı zehirleme etkisiz), aynı yanıt metni, hız sınırı · 2FA: 5 hatada 15 dk kilit,
yedek kod tek kullanım, TOTP ±1 adım sabit zamanlı karşılaştırma, yarım (kod bekleyen) oturum panele girmez (tüm /admin 302), QR sayfası
`no-store`, kapatma parola + antiforgery · dosyalar: magic-byte PDF/PNG/JPEG/WebP, SVG/HTML/JS'li dosya reddi, 5 MB, GUID ad (çakışma/yol
geçişi yok), wwwroot dışı, `/private` ve `%2e%2e` 404, fatura yalnız tam anahtarla (IDOR yok), `Content-Disposition` + `nosniff` ·
manuel sipariş/düzenleme: adet < 1 red, fiyat sunucudan, Site kaynağı/kart red, eksi kargo red, over-post etkisiz, atomik stok, denetim izi
eski→yeni · havale bildirimi: XSS kaçışlı, tutar farkında otomatik onay yok, 6. istek 429, dosya hattı aynı · iade: antiforgery, çift iade
koşullu UPDATE, ödenmemiş sipariş red, tutar kayıttan · Excel: 8 MB, imza, başlık, 5000 satır, hep-ya-hiç, görsel köken beyaz listesi, XXE
(ClosedXML DTD kapalı), dışa aktarma hücreleri metin (formül değil) · rapor/CSV: girişsiz 302, uzun aralık tek sorgu · sipariş sorgulama:
aynı yanıt, 11. istek 429, 0/90/+90 telefon biçimleri aynı sonuç. AV-1'in 20 başlığı: S01-S04, S06, S07, S09-S11, S13, S15, S20 korunuyor;
S05, S12 iyileşti; S14, S16 kısmi → aşağıda.

ID | şiddet | dosya:satır/URL | bulgu | karar
---|---|---|---|---
S-01 | O | HerYerde.Web/Infrastructure/CsvFile.cs:31 | CSV formül enjeksiyonu: baştaki `-` (ör. `-2+3+cmd\|' /C calc'!A0`) kaçışlanmıyordu | DÜZELTİLDİ — `=`, `+`, `-`, `@`, TAB, CR ile başlayan alan `'` alır (OrderExportTests.Csv_basi_eksi_arti_esittir_ve_et_ile_baslayan_alani_notrler)
S-02 | D | HerYerde.Web/Areas/Admin/Controllers/AuthController.cs:125 | Şifremi unuttum: kayıtlı e-posta medyan 6,1 ms, kayıtsız 2,8 ms — zamanlamayla hesap varlığı okunabiliyordu (AV-1 S16 kısmi) | DÜZELTİLDİ — yanıt en az 400 ms (AdminAccountTests.Sifremi_unuttum_kayitli_ve_kayitsiz_epostada_ayni_asgari_surede_doner)
S-03 | D | HerYerde.Web/Infrastructure/ProductSheet.cs:97 | xlsx zip bombası: 8 MB altı dosya yüzlerce MB'a açılıp belleği tüketebiliyordu | DÜZELTİLDİ — zip girişlerinin açılmış toplamı 64 MB'ı aşarsa okunmadan 400 (ProductTransferTests.Acilmis_boyutu_tavani_asan_xlsx_okunmadan_400)
S-04 | D | HerYerde.Entities/Concrete/AdminUser.cs:30 | TOTP gizli anahtarı düz metin (veritabanı ve yedek) | KABUL — doğrulama anahtarın kendisini ister (özetlenemez); DataProtection ile şifrelemek anahtar halkası kaybında yöneticiyi kilitler. Erişim veritabanı/yedek izniyle sınırlı, `--admin-sifirla` kurtarma yolu; envanterde yazılı
S-05 | D | AV-1 S14 | Sıfırlama bağlantısının HTTPS olması `Shop:BaseUrl` yapılandırmasına bağlı | KABUL — Host başlığından türetilmez (zehirleme yok); prod `HERYERDE_BASE_URL` https ile verilir (`docs/yayin-kontrol.md`)

Medium+ güvenlik bulgusu: elle önce 0 · ZAP önce 2 (Z-06 doğrulanmış yanlış pozitif; Z-03 enjeksiyon değil, eşzamanlı slug 500'ü) · sonra 0.

## 3. Bağımlılıklar
`dotnet list package --vulnerable --include-transitive`: 6 projede açık yok · `npm audit`: 0 · ClosedXML 0.105.1, QRCoder 1.8.0,
Sentry/Sentry.Serilog 6.11.0 son sürüm ve advisory yok; geçişli SixLabors.Fonts 1.0.0, DocumentFormat.OpenXml 3.1.1 advisory yok; ImageSharp
3.1.12 bilinen 7 advisory'nin hepsi kapalı; .NET 10.0.11 Eylül advisory'lerinden etkilenmiyor. CI security-scan yeşil (7bdbc13 push ve elle).

ID | şiddet | dosya:satır/URL | bulgu | karar
---|---|---|---|---
DEP-01 | D | HerYerde.Web/HerYerde.Web.csproj (SixLabors.ImageSharp 3.1.12) | 3.x hattının son sürümü; yamalar ileride yalnız 4.x'e gelebilir | KABUL — 4.x major + lisans değişikliği; haftalık security-scan izler, yamasız 3.x advisory'sinde geçilir
DEP-02 | D | EF Core / Identity.Core / Mvc.Testing 10.0.11 | 10.0.12 servis sürümü var, 10.0.11'i etkileyen advisory yok | KABUL — rutin bakımda runtime ile birlikte
DEP-03 | D | SixLabors.Fonts 1.0.0, DocumentFormat.OpenXml 3.1.1 (ClosedXML geçişli) | Son sürümlerin gerisinde, advisory yok | KABUL — ClosedXML sabitliyor; doğrudan zorlamak uyumsuzluk riski
DEP-04 | D | tests: coverlet.collector, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio | Major sürüm gerisinde, yalnız test aracı | KABUL — üretime gitmez, advisory yok
DEP-05 | D | .github/workflows/ci.yml:187 | security-scan NuGet adımı İngilizce metin grep'ine dayanıyordu; SDK mesajı değişirse ya da restore sorunu olursa sessizce yeşil kalırdı | DÜZELTİLDİ — `--format json` + jq: açıklı paket ve proje/tarama sorunu sayılır; filtre önce açıklı örnekte denenir (yerelde örnek 1, sorun 1, temiz 0; eski grep değişmiş metinde yeşil kalıyordu)

## 4. ZAP
Ayrıntı `docs/zap-3.md`. Vitrin (baseline + sipariş sorgula + teşekkür/havale bildirimi/fatura sayfası + full) ve yönetim (tarama hesabı
çerezi, Automation Framework planı; oturumlu tarandığı web logundan denetlendi).

ID | şiddet | dosya:satır/URL | bulgu | karar
---|---|---|---|---
Z-01 | D | http://localhost:8080/ (90004) | Cross-Origin-Embedder-Policy başlığı yok | KABUL — ZAP-1/2 ile aynı: `require-corp` dış ürün görsellerini kırar
Z-02 | O | POST http://localhost:8080/admin/categories/create, /admin/products/create, /admin/orders/new (ilk yönetim taraması 1380 × 500) | Onay kutusuna bool olmayan değer (`IsActive=zap`) gönderilince form yeniden çizilirken InputTagHelper FormatException atıyor, 500 dönüyordu (90022 Application Error Disclosure) | DÜZELTİLDİ — `CheckboxBoolModelBinder`: bozuk değer doğrulama hatası, ModelState'e false (CheckboxBindingTests.Onay_kutusuna_bool_olmayan_deger_500_vermez_form_yeniden_cizilir; son taramada FormatException 0, 5xx 1380 → 22)
Z-03 | D | POST http://localhost:8080/admin/categories/create, /edit/2 (4-9 × 500; son yönetim koşusunda ZAP bunu 40018 SQL Injection, High/Low güven sanıyor: saldırı `ZAP'(` adından türeyen slug) | Aynı adla eşzamanlı kaydetmede slug denetimi ikisinde de boş görüyor, ikinci kayıt `ux_category_slug` indeksine çarpıp DbUpdateException → 500 veriyordu; sorgular parametreli, SQL enjeksiyonu yok | DÜZELTİLDİ — kayıtta indeks çakışması aynı slug'ın varlığıyla doğrulanıp 409 "yeniden deneyin" döner (CategorySlugRaceTests.Denetimden_sonra_ayni_slug_yazilmissa_kayit_409_doner_istisna_firlatmaz — yarış bayat denetimle belirlenimli canlandırılır)
Z-04 | D | GET http://localhost:8080/admin/denetim, /admin/mesajlar, /admin/yorumlar (2 Private IP Disclosure) | Denetim izi işlemi yapanın IP'sini (tarayıcı: Docker köprüsü 172.18.0.1) gösteriyor; mesaj/yorum listesindeki `172.18.0.1:44717` sabit portlu ZAP geri çağırma (OAST) adresi olarak saldırı yüküyle metne girmiş | KABUL — denetim izinde tasarım gereği ve yalnız yöneticiye; mesaj/yorum görünümlerinde IP alanı yok (IP saklanmaz), değer kullanıcı metni
Z-05 | D | POST http://localhost:8080/admin/mesajlar/{id}/okundu, /admin/yorumlar/{id}/onayla (18 × 500, 90022) | Tarayıcı aynı saniyede kaydı silip (302) okundu/onayla gönderince DbUpdateConcurrencyException → 500 | KABUL — yalnız aynı kayda eşzamanlı sil + güncelle yarışında; veri değişmez, sayfada ayrıntı yok (yalnız durum satırı), tek yöneticili panelde gerçekçi değil
Z-06 | Y (ZAP High, güven Low) | POST http://localhost:8080/admin/categories/edit/1, /edit/2 (6 Path Traversal, 6-5) | `Id`/`SortOrder` parametresinde URL dosya adı (`1`/`2`) saldırısı; kanıt boş | KABUL (yanlış pozitif) — 6-5 sezgisi yalnız durum farkına bakar; parametreler `int`, EF'e gider, dosya yoluna ulaşmaz (tek dosya işlemi `SaveCategoryAsync(int id)`), içerik imzası kuralları (6-1..6-4) tetiklenmedi, yanıtta dosya içeriği yok; dalgalanma formun kendi 200 (geçersiz)/302 (başarılı)/400 kuralı. Planda kural 6 + bu URL + bu iki parametreyle `alertFilter` ile yanlış pozitif işaretlendi

Medium+: önce 2 (Z-06 yanlış pozitif, Z-03'ün 500'ü) · sonra 0 (son tam koşu 35005969698: vitrin + oturumlu yönetim, Low 3 tür, 5xx yalnız Z-05'teki 18 eşzamanlılık yanıtı).

## 5. Yasal / veri
Envanter (`docs/veri-envanteri.md`) yeni tablolarla güncel: `payment_notice` (+ dekont dosyası), `slug_history`, fatura PDF'i ve gizli klasör
(`/app/private`), 2FA (`totp_secret` düz metin, `totp_last_step`, yedek kodlar ve sıfırlama anahtarı SHA-256 özet), yedekler. Anonimleştirme
fatura (korunur, anahtar yenilenir), dekont (silinir) ve denetim izini (ayrıntı silinir) kapsar; 2FA yönetici verisidir, sipariş
anonimleştirmesinin konusu değil. Fatura saklama 10 yıl (TTK m. 82, VUK m. 253). Gece yedeği gizli klasörü `belgeler-*.tar.gz` olarak içerir
(14 gün). ETBİS bandı `Legal:EtbisNo` doluyken görünür — doğru.

ID | şiddet | dosya:satır/URL | bulgu | karar
---|---|---|---|---
V-01 | Y | HerYerde.Web/Views/Legal/kvkk-aydinlatma.cshtml:30; HerYerde.Web/Views/Checkout/ThankYou.cshtml:100 | Havale bildirimi ve dekont aydınlatmada yoktu; bildirim formunda aydınlatma bağlantısı yoktu | DÜZELTİLDİ — işlenen veri/amaç/saklama maddeleri, formda bağlantı; sürüm `2026-09-15.2` (LegalTextsD11Tests.Aydinlatma_havale_bildirimi_dekont_fatura_ve_kart_iadesini_anlatir, Havale_bildirim_formu_aydinlatmaya_baglanir_ve_surum_ilerler)
V-02 | Y | HerYerde.Web/Views/Legal/kvkk-aydinlatma.cshtml:63; gizlilik-politikasi.cshtml:23 | WhatsApp/Instagram/telefon/mağaza kanalından toplama anlatılmıyor, toplama yöntemi yalnız "otomatik"; Meta anılmıyor | DÜZELTİLDİ — kanal maddesi, "kısmen otomatik" yöntem, Meta Platforms notu (Aydinlatma_ve_gizlilik_manuel_siparis_kanallarini_anlatir). Yurt dışı aktarım (KVKK m. 9) nitelendirmesi hukukçu teyidine bırakıldı (`docs/yasal-notlar.md` sürecinde)
V-03 | Y | HerYerde.Business/Concrete/OrderManager.cs:260; mesafeli-satis-sozlesmesi.cshtml:28,72 | Uzaktan kanal manuel siparişinde ön bilgilendirme/teyit kaydı yoktu (`consent_at` boş), sözleşme yalnız siteyi kapsıyordu | DÜZELTİLDİ — Mağaza dışı kaynakta "iletildi, müşteri teyit etti" kutusu zorunlu, teyit anı + metin sürümü yazılır; sözleşme uzaktan siparişi kapsar (ManualOrderTests.Uzaktan_kanal_siparisi_teyit_olmadan_acilmaz_teyit_ani_ve_surum_kaydedilir, Mesafeli_sozlesme_whatsapp_ve_telefon_siparisini_kapsar)
V-04 | Y | mesafeli-satis-sozlesmesi.cshtml:62; teslimat-ve-iade.cshtml:28,59 | İade metni yalnız "havale ile IBAN'a"; kartla ödemenin iadesi aynı karta (MSY m. 13/2); teslimat sayfası kartı saymıyor | DÜZELTİLDİ — yönteme göre iade, "Kartla ödeme" maddesi; kapıda kart iadesi `[MÜŞTERİ]` yer tutucusu (Iade_metinleri_karta_iadeyi_ve_kartla_odemeyi_anlatir). Altbilgi notu (`_Layout.cshtml:116`, "Kapıda ödeme & havale/EFT"): KABUL — kart yapılandırmaya bağlı sunulur, not her zaman var olan yöntemleri söyler
V-05 | O | HerYerde.Business/Concrete/OrderManager.cs:759; HerYerde.Web/Areas/Admin/Controllers/OrdersController.cs:327 | Anonimleştirmede dekont (ad, IBAN) 10 yıl kalıyordu | DÜZELTİLDİ — dekont dosyası silinir, kayıttan düşer (AnonymizeDocumentsTests)
V-06 | O | HerYerde.Web/Areas/Admin/Controllers/OrdersController.cs:330; kvkk-aydinlatma.cshtml:81 | Düzenleme izinde eski/yeni adres-telefon anonimleştirmeden sonra kalıyordu; aydınlatmada yoktu | DÜZELTİLDİ — anonimleştirmede siparişin iz ayrıntısı silinir (işlem/yönetici/zaman kalır); aydınlatmada "eski ve yeni değer" (AnonymizeDocumentsTests, Aydinlatma_islem_kaydi_ve_yedek_suresini_anlatir)
V-07 | O | docs/veri-envanteri.md:34; kvkk-aydinlatma.cshtml:82 | Envanter "yedekleme politikası tanımlı değil" diyordu; aydınlatmada yedek süresi yok | DÜZELTİLDİ — yedek satırı (14 gün, şifresiz, geri yüklemede anonimleştirmelerin yinelenmesi), aydınlatmada "Yedekler: en çok 14 gün"
V-08 | O | docs/veri-envanteri.md:18 | TOTP anahtarının düz metin olduğu envanterde yazmıyordu | DÜZELTİLDİ — envanterde düz metin + gerekçe (bkz. S-04)
V-09 | O | kvkk-aydinlatma.cshtml:30,74 | Fatura belgesi veri türü ve saklama dayanağı (TTK m. 82, VUK m. 253) yoktu | DÜZELTİLDİ — aynı test (V-01)
V-10 | D | HerYerde.Business/Concrete/OrderManager.cs:749 | Anonimleştirmeden sonra `access_token` aynı; eski bağlantıyla ad/adresli fatura PDF'i iniyordu | DÜZELTİLDİ — anahtar yenilenir, eski bağlantı 404 (AnonymizeDocumentsTests)
V-11 | D | docs/veri-envanteri.md | Havale onay postası envanterde adıyla yoktu | DÜZELTİLDİ — satır eklendi
V-12 | D | docs/veri-envanteri.md | Bekleyen içe aktarma dosyası yeni yükleme olmazsa kalır ve belge yedeğine girer; envanter demiyordu | DÜZELTİLDİ — satıra eklendi (kişisel veri içermez)
V-13 | D | HerYerde.Web/Views/Legal/cerez-politikasi.cshtml:55 | Yönetici çerezi "8 saat"; kod 12 saat kayar | DÜZELTİLDİ — "12 saat (işlem yaptıkça uzar)" (Cerez_politikasi_yonetici_oturumunu_12_saat_yazar)
V-14 | D | HerYerde.Business/LegalDocs.cs:7 | Metin aynı gün değişti, sürüm ilerlemedi | DÜZELTİLDİ — `2026-09-15.2`
V-15 | D | kvkk-aydinlatma.cshtml:63 | İyzico aktarım amacı yalnız "ödemenin alınması" (iade de var) | DÜZELTİLDİ — "alınması ve iadesi"

## 6. SEO / A11y
Lighthouse 12 mobil (perf/a11y/bp/SEO): `/` 99/100/100/100 · `/ev` 96/100/100/100 · varyantlı Ev ürünü (`/urun/tac-sera-feel-3lu-sahan-seti`)
100×4 · `/ortu` 99/100/100/100 · `/siparis-sorgula` önce 100/100/100/66 → sonra 100×4 · `/hakkimizda` 100×4 · görselli kategori
(`/ev/tencere-tava`) 100×4. 96 görselin hepsinde width/height; boş alt yalnız yanında metin olan dekoratif görselde (kapı başlığı, bant
h1, kart medyası aria-hidden). Slug 301: eski ürün/kategori adresi yeni adrese (sorgu korunur), sitemap'te (51 URL) eski slug yok. JSON-LD
51 URL'de ayrıştırma hatası 0 (Product, BreadcrumbList, Organization, FAQPage). Kırık bağlantı: vitrin 87 + yönetim 145 URL (girişli), 4xx/5xx 0.
Görünüm değişikliği (manuel sipariş teyit kutusu, havale formu aydınlatma bağlantısı): frontend-craft tur 2'de 390/768/1440 sapma 0, audit PASS.

ID | şiddet | dosya:satır/URL | bulgu | karar
---|---|---|---|---
SEO-01 | O | HerYerde.Web/Controllers/SeoController.cs:23 | robots.txt `Disallow: /siparis` önek eşleşmesiyle `/siparis-sorgula`'yı da kapatıyordu (LH SEO 66, is-crawlable) | DÜZELTİLDİ — `/siparis/` (SeoTests.Robots_siparis_sorgula_sayfasini_kapatmaz; LH 100)
SEO-02 | D | HerYerde.Web/Views/Store/Category.cshtml | Kategori sayfasında BreadcrumbList JSON-LD yok | KABUL — görünür içerik bağlantısı var, ürün sayfaları BreadcrumbList taşır; SEO 100
SEO-03 | D | 19 görselsiz ürün | Product JSON-LD `image` yok | KABUL — veri işi (görsel yüklenince kendiliğinden gelir)
SEO-04 | D | HerYerde.Web/Views/Store/Category.cshtml:20 | Bant görselinde `fetchpriority`/`srcset` yok; `/ev` perf 96 (LCP 2,8 s) | KABUL — hedef ≥95 karşılanıyor
SEO-05 | D | HerYerde.Web/Models/ProductImages.cs:10 | Kart `sizes` mobilde 92vw, kart 180 px; fazla indirme | KABUL — perf hedefleri karşılanıyor; görsel hattı ayrı iş
SEO-06 | D | HerYerde.Web/Views/Store/Index.cshtml:59,74 | Kapı görsellerinde `srcset` yok | KABUL — `/` perf 99
UI-01 | D | src/input.css (`.switch`) | Tur 1: uzun etiketli teyit kutusu 13 px'e küçülüyor, ikinci satıra hizalanıyordu; havale formundaki bağlantı metinden ayırt edilmiyordu | DÜZELTİLDİ — `flex-shrink:0` + ilk satıra hizalama; bağlantı marka satır içi bağlantı grubunda (tur 2: 20×20, #8A361F altı çizili, odak halkası)
