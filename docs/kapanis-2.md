# KAPANIŞ-2 — D6-D9 sonrası kapanış listesi (15 Eylül 2026)

Ortam: `http` profili (Development, HerYerdeVitrin LocalDB), sahte İyzico (yerel HTTP, gerçek imza/HMAC), gerçek HTTP istekleri
(node betiği, 98 kontrol). ZAP: GitHub Actions, prod compose (bkz. `docs/zap-2.md`). Şiddet: Y yüksek · O orta · D düşük. Satır biçimi:
`ID | şiddet | yer | bulgu | karar`.

## 1. Temel fonksiyon testleri
Mevcut 454 test yeşil (başlangıç) → 468 yeşil (14 yeni kırmızı-önce test/vaka). Uçtan uca 89 kontrol, düzeltmeden sonra 89/89:
Ev kapıda (1×1290 → 79,90 kargo, stok −1, token'sız teşekkür 404, 2 posta, e-postadaki token doğru sipariş) · havale eşik üstü
(2580, kargo 0, IBAN) · Giyim varyantlı (varyant stoğu ve SKU) · kart/sahte sağlayıcı (3D formu, onayda stok) · iptal + stok iadesi ·
admin zinciri (atlama 400, takip nosuz 400, teslim, anonimleştir) · iletişim formu · yorum gönder-onayla · arama/filtre/sayfalama
(`sayfa=999/-1/abc` 5xx yok) · 6 yasal sayfa + /hakkimizda /sss /iletisim · 404 markalı · /hata/500 iz yok.

F-VAR | Y | HerYerde.Business/Concrete/CartManager.cs:166 | Varyantlı ürün (`POST /sepet/ekle productId=31`, variantId yok) 302 ile sepete giriyor, sipariş beden/renksiz ve stok denetimsiz açılıyordu | DÜZELTİLDİ — varyantlı üründe seçimsiz ekleme 400 (CartManagerTests.Varyantli_urun_varyant_secilmeden_sepete_eklenemez)
F-CARD-1 | Y | HerYerde.Business/Concrete/OrderManager.cs:309 | Ödemesi tamamlanmamış kartlı siparişin admin iptali, hiç düşmemiş stoğu artırıyordu (canlı: 7 → 8) | DÜZELTİLDİ — açık ödeme kaydı iptalde kapatılır, stok yalnız çekim geçmişse iade edilir (CardPaymentTests.Odenmemis_kart_siparisi_iptal_edilince_stok_artmaz_ve_gec_donus_cekim_yapmaz)
F-CARD-2 | Y | HerYerde.Business/Concrete/OrderManager.cs:309 | İptal edilmiş siparişe sonradan gelen imzalı 3D dönüşü çekim yapıyordu (canlı: ödeme Başarılı, sipariş İptal) | DÜZELTİLDİ — aynı test; iptal ödemeyi Başarısız kapatır, geç dönüş sağlayıcıya auth çağrısı yapmaz
F-CARD-3 | O | HerYerde.Business/Concrete/OrderManager.cs:274 | Parası alınmamış kartlı sipariş Onaylandı'ya (ve ardından kargoya) geçebiliyordu | DÜZELTİLDİ — 400 "Kart ödemesi alınmamış sipariş onaylanamaz" (CardPaymentTests.Odenmemis_kart_siparisi_onaylanamaz)
F-REFUND | D | HerYerde.Business/Concrete/OrderManager.cs:309 | Kartla ödenmiş siparişin iptali stoğu iade eder ama parayı İyzico'dan geri ödemez | KABUL — D7 kapsamı iade düğmesi içermez (mevcut test "İade et" yok); iade İyzico panelinden elle
F-FAV | D | http://localhost:5250/favicon.ico | Tüm sayfalarda 404 konsol hatası; Lighthouse best-practices 96 | KABUL — marka favicon varlığı yok (brand.md'de tanımsız), best-practices için hedef verilmedi

## 2. Güvenlik ve sızma
Canlı doğrulanan, bulgu çıkmayan yüzeyler: iletişim formu XSS/HTML (admin listesi ve mağaza postası kaçışlı), honeypot dolu → 200 kayıt yok,
tokensız POST 400 · yorum XSS (vitrin, admin, JSON-LD kaçışlı), onaysız yorum vitrin/ana sayfa/JSON-LD'de yok, puan 9 ve −1 → 400,
başka ürünün sipariş no'su rozet vermez, `ProductId=31&IsApproved=true` enjeksiyonu yok sayılır · outbox: şablon düz dize + kaçış,
URL'ler yapılandırmadan (SSRF yok), günlükte yalnız sayılar, token'lı bağlantı doğru siparişin · ödeme dönüşü: tekrar oynatma aynı
sonuç + tek auth, sahte imza reddedilir (ödeme Başarısız, sipariş İptal), rastgele conversation_id 404, mdStatus≠1 red, paidPrice
oynatma red + stok geri, 5 eşzamanlı dönüş → tek çekim · admin mesaj/yorum ekranları girişsiz 302, tokensız POST 400 · takip no
`"><script>` admin/teşekkür/posta kaçışlı · dosya yükleme D5'ten beri yalnız dolgu rengi değişti (magic byte + 8 MB + 20/dk) ·
admin çerezi HttpOnly + SameSite=Lax (prod Secure) · hız sınırı 8 kovanın hepsi eşikte 429.

AV-1 (audit-1 A) yeniden: S01 KORUNUYOR · S02 KORUNUYOR (`Html.Raw` yalnız JSON-LD, JavaScriptEncoder) · S03 KORUNUYOR (yeni 7 POST
global antiforgery; 3D dönüşü bilinçli `IgnoreAntiforgeryToken` + imza) · S04 KORUNUYOR · S06 KORUNUYOR · S09 KORUNUYOR (3D dönüşü
sabit hedefe 303) · S13 GERİLEME → S-XFF · S15 DEĞİŞTİ → S-UPL · S19/S20 KORUNUYOR (ödeme ham yanıtı maskeli) · diğerleri değişmedi.

S-XFF | O | HerYerde.Web/Program.cs:120 | `KnownProxies`/`KnownIPNetworks` boşaltılınca ForwardedHeaders her kaynağın X-Forwarded-For'unu kabul ediyordu; başlığı döndürerek tüm hız kovaları (admin girişi, iletişim, yorum, ödeme) atlatılıyordu (canlı: 8 × 200) | DÜZELTİLDİ — vekil tanımlı değilse `ForwardedHeaders.None` (RateLimitTests.X_Forwarded_For_degistirerek_odeme_sinirindan_kacilamaz; canlı: 200×5, 429×3). Prod TLS vekili arkasında `ForwardedHeaders__KnownProxies__0` verilmeli
S-CRLF | D | HerYerde.Business/Notifications/NotificationTemplates.cs:82 | İletişim formundaki ad CR/LF ile posta konusuna yazılıyordu; MimeKit başlıkta CR/LF'yi attığı için gerçek başlık enjeksiyonu yok (test projesinde serileştirme ile doğrulandı) | DÜZELTİLDİ — konuda denetim karakteri boşluğa çevrilir (ContactFormTests.Addaki_satir_sonu_posta_konusuna_girmez)
S-REDIR | D | HerYerde.Web/Controllers/CartController.cs:81 | ASCII dışı `donus` / admin `returnUrl` (`/ş`) `Url.IsLocalUrl`'u geçip Location başlığında Kestrel 500'ü üretiyordu (ZAP 90022) | DÜZELTİLDİ — yalnız yazdırılabilir ASCII yerel adres (CartQuantityTests.Ascii_disi_donus_adresi_sepete_yonlenir, AdminAuthorizationTests.Ascii_disi_return_url_urun_listesine_yonlenir)
S-NUL | D | ASP.NET Core form okuma (tüm POST formları) | `%00` içeren form gövdesi yanıt yerine bağlantı kapanmasıyla sonuçlanıyor (Kestrel `Reading is already in progress`) | KABUL — çerçeve davranışı; yalnız isteği gönderene etki eder, veri değişmez, iz sızmaz; ZAP bulgu üretmedi
S-HP | D | HerYerde.Web/Controllers/ContactController.cs:20 | Honeypot alanını hiç göndermeyen bot geçer | KABUL — tuzak yalnız form dolduran botlar için; asıl sel koruması 5/dk iletişim kovası (S-XFF'den sonra fiilen çalışıyor)
S-UPL | D | HerYerde.Web/Infrastructure/ProductImageStorage.cs:40 | ImageSharp'ta piksel üst sınırı yok; 8 MB'lık görsel çok büyük boyuta açılabilir | KABUL — yalnız yönetici, 20/dk yükleme kovası
S-SIG | D | HerYerde.Web/Infrastructure/IyzicoPaymentProvider.cs:132 | Auth yanıtında imza alanı yoksa yanıt imzasız kabul edilir | KABUL — yanıt sunucudan İyzico'ya TLS çağrısının dönüşü, istemci kontrolünde değil; tutar sipariş toplamıyla ayrıca karşılaştırılır

## 3. Bağımlılık güvenliği
`dotnet list package --vulnerable --include-transitive`: 6 projede açık yok · `npm audit`: 0 · MailKit 4.18.0 / MimeKit 4.18.0 /
BouncyCastle 2.7.0: bilinen advisory yok · İyzico SDK kullanılmıyor (doğrudan HTTP + HMAC) · CI security-scan: yeşil (run 34899443752, 34902743307).

DEP-IS | D | HerYerde.Web/HerYerde.Web.csproj | SixLabors.ImageSharp 3.1.12 → 4.1.2 mevcut | KABUL — 3.1.12 bilinen CVE'siz (GHSA 3.1.11'de kapandı); 4.x ana sürüm/lisans değişikliği ayrı iş

## 4. OWASP ZAP
Ayrıntı `docs/zap-2.md`. Baseline + full (context /admin ve /odeme/3d-donus dışarıda, sepet çerezi, taramaya özel ürün tohumu):
ilk tarama Medium+ 0, Low 2; tekrar tarama Medium+ 0, Low 1.

Z-90022 | D | POST http://localhost:8080/sepet/ekle | Application Error Disclosure (500) | DÜZELTİLDİ → S-REDIR
Z-90004 | D | http://localhost:8080/ | Cross-Origin-Embedder-Policy yok | KABUL — `require-corp` dış ürün görsellerini kırar (ZAP-1 ile aynı)
CI `zap-scan` (workflow_dispatch): yeşil, Medium+ 0 (run 34903864800; aynı koşuda build-test, security-scan, browser-check yeşil)

## 5. Yasal ve veri yönetimi
V-KVKK | Y | HerYerde.Web/Views/Legal/kvkk-aydinlatma.cshtml:31 | D7'den beri kartla ödeme varken aydınlatma, gizlilik ve ön bilgilendirme "Kart bilgisi istemeyiz / internet üzerinden alınmaz" diyordu; İyzico ve e-posta sağlayıcısına aktarım, iletişim formu ve yorum işlemesi anlatılmıyordu | DÜZELTİLDİ — 5 metin güncellendi, `LegalDocs.Version` 2026-09-15 (yeni siparişler yeni sürümü onaylar) (LegalPagesTests.Yasal_metinler_kart_odemesini_eposta_iletisim_ve_yorum_islemesini_anlatir)
V-RET | O | HerYerde.Business/Concrete/NotificationManager.cs:140 | `outbox_message` (alıcı e-postası, ad, token'lı bağlantı) ve `contact_message` için saklama süresi/temizlik işi yoktu (envanterde `[MÜŞTERİ: saklama süresi]`) | DÜZELTİLDİ — `PersonalDataCleanupHostedService`: posta 30 gün, iletişim mesajı 1 yıl (OutboxTests.Otuz_gunu_dolan_..., ContactFormTests.Yili_dolan_..., OutboxTests.Kisisel_veri_temizligi_gece_isi_olarak_kayitlidir)
V-ANON | O | HerYerde.Business/Concrete/OrderManager.cs:375 | Anonimleştirilen siparişin postaları ad, e-posta ve token'lı bağlantıyla kalıyordu (canlı: 2 kayıt) | DÜZELTİLDİ — anonimleştirme siparişin postalarını siler (OrderAnonymizeTests.Anonimlestirme_siparisin_postalarini_da_siler)
V-CEREZ | D | HerYerde.Web/Views/Legal/cerez-politikasi.cshtml:43 | Yorum/ödeme bildirimi `.AspNetCore.Mvc.CookieTempDataProvider` çerezini bırakıyor, politikada yoktu | DÜZELTİLDİ — tabloya satır (LegalPagesTests.Cerez_politikasi_...)
V-ENV | D | docs/veri-envanteri.md | `payment`, `outbox_message` satırı, TempData çerezi, aktarım ve temizlik işleri envanterde yoktu | DÜZELTİLDİ (LegalPagesTests.Veri_envanteri_yeni_tablolari_cerezi_ve_temizlik_islerini_kapsar)
V-REV | D | docs/veri-envanteri.md | Anonimleştirmede `product_review` (görünen ad + sipariş no) kalır | KABUL — ad kullanıcının yayımlanmasını istediği görünen ad, sipariş no anonim siparişe bağlanır; kaldırma yönetimden
V-PAY | D | docs/veri-envanteri.md | Anonimleştirmede `payment.raw_response` kalır | KABUL — kart numarası/CVC maskeli, alıcı verisi saklanmıyor; mutabakat için 10 yıl
E-postalarda PII: müşteri postası ad + sipariş özeti, mağaza postası ad + il/ilçe (telefon/adres yok) — gerektiği kadar. KVKK başvuru akışı aydınlatma metninde (kanal yer tutucuları müşteride).

## 6. SEO, A11y & kapanış
Lighthouse 12 (masaüstü değil, varsayılan mobil; http://localhost:5250):
/ perf 99 · a11y 100 · bp 96 · seo 100 — /ev 96 · 100 · 96 · 100 — /urun/ipek-gorunumlu-desenli-esarp 100 · 100 · 96 · 100 —
/ortu 100 · 100 · 96 · 100 — /hakkimizda 100 · 100 · 96 · 100 — /sss 100 · 100 · 96 · 100 — /iletisim 100 · 100 · 96 · 100
(bp tek kusur favicon 404 → F-FAV). Hedefler (a11y/SEO 100, perf ≥ 95) tuttu.
Sitemap: 23 statik + ürünler; /hakkimizda /sss /iletisim /ortu + 5 alt kategori + 6 yasal sayfa var. JSON-LD: 67 sayfada Organization 1,
Product 28, BreadcrumbList 28, FAQPage 1, AggregateRating (onaylı yorumlu üründe) — hepsi parse edilir, zorunlu alanlar dolu, 0 hata.
Kırık iç link: sitemap'ten başlayan 67 URL taraması, 0 kırık. frontend-craft turu: görünüm değişmedi (yalnız yasal metin içeriği).
