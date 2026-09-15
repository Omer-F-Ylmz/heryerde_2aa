# Veri envanteri (KAPANIŞ-5, KAPANIŞ-2)

Hangi kişisel veri nerede, ne kadar tutulur ve nasıl silinir. Metinler: `/yasal/kvkk-aydinlatma`, `/yasal/gizlilik-politikasi`,
`/yasal/cerez-politikasi`.

Veri | Nerede | Amaç | Süre | Silme / anonimleştirme
---|---|---|---|---
Sipariş kişisel verisi: ad soyad, cep telefonu, e-posta, adres, il/ilçe, sipariş notu | `order` tablosu | Sözleşmenin kurulması ve ifası, teslimat, yasal saklama | 10 yıl (6502 / VUK) | Yönetim → sipariş detayı → "Kişisel veriyi anonimleştir"; yalnız Teslim edildi / İptal edildi siparişte. Ad `A*** Y***`, telefon `05*******82`, e-posta `a***@***`, adres `***`, not silinir; il/ilçe ve tutarlar kalır; siparişin `outbox_message` postaları silinir; denetim izi yazılır
Onay kaydı: `consent_at`, `legal_version` | `order` tablosu | Ön bilgilendirme ve mesafeli satış onayının ispatı | Siparişle birlikte | Anonimleştirmede korunur (kişisel veri değil)
Sipariş satırları: ürün adı, stok kodu, adet, fiyat | `order_item` | Sözleşme, fatura, stok | Siparişle birlikte | Kişisel veri içermez
Kart ödemesi kaydı: sağlayıcı, conversation_id, İyzico payment_id, tutar, durum, sağlayıcı yanıtı | `payment` | Ödemenin alınması, mutabakat, iade | Siparişle birlikte (10 yıl) | Kart numarası ve CVC hiç yazılmaz; `raw_response`'ta `cardNumber`/`binNumber` (son 4 hane hariç), `cvc`, `cardHolderName` maskelenir, 3DS HTML'i saklanmaz, 4000 karaktere sığmazsa üst düzey dizi/nesneleri atılır (geçerli JSON kalır). Alıcı adı/adresi yanıtta tutulmaz; anonimleştirmede kişisel veri kalmadığından dokunulmaz
E-posta bildirimi: alıcı e-postası, konu, gövde (ad, sipariş özeti, token'lı teşekkür bağlantısı; iletişim mesajında ad, iletişim bilgisi, mesaj) | `outbox_message` | Sipariş/kargo/iletişim bildirimi | Gönderimden (başarısızsa son denemeden) sonra 30 gün; bekleyen kayıt gönderilene kadar | `PersonalDataCleanupHostedService` gecede bir siler; sipariş anonimleştirilince o siparişin postaları hemen silinir. Günlüğe yalnız sayılar yazılır
Anonim sepet ve satırları | `cart`, `cart_item`; kimlik `heryerde.cart` çerezinde | Alışveriş sepeti | 30 gün | `CartCleanupHostedService` gecede bir siler
Son sipariş anahtarı | `heryerde.lastorder` çerezi | Teşekkür sayfasının bağlantısız açılması | 30 gün | Tarayıcıda süresi dolar
CSRF anahtarı | `.AspNetCore.Antiforgery.*` çerezi | Form güvenliği | Tarayıcı oturumu | Tarayıcı kapanınca
Tek seferlik bildirim (yorum alındı, ödeme reddi) | `.AspNetCore.Mvc.CookieTempDataProvider` çerezi | Yönlendirme sonrası mesaj | Tarayıcı oturumu | Okununca silinir; kişisel veri taşımaz
Yönetici hesabı: e-posta, parola özeti, hatalı giriş/kilit | `admin_user` | Yönetim paneline erişim | Hesap süresince | Elle
Yönetici oturumu | `heryerde.admin` çerezi | Yönetim oturumu | 8 saat (kayar) | Çıkışta / süre dolunca
İletişim formu mesajı: ad soyad, e-posta ya da telefon, konu, mesaj | `contact_message` + mağazaya giden e-posta (`outbox_message`) | Müşteri sorusuna yanıt | 1 yıl (varsayılan; `PersonalDataCleanupHostedService.ContactMessageMaxAge`) | `PersonalDataCleanupHostedService` gecede bir 365 günden eskiyi siler; Yönetim → Mesajlar → "Sil" hemen siler, denetim izi yazılır. IP saklanmaz; bot tuzağına takılan gönderim hiç kaydedilmez
Ürün yorumu: görünen ad, puan, yorum, isteğe bağlı sipariş numarası | `product_review` | Ürün değerlendirmesinin yayını | Ürün kaydıyla birlikte | Yönetim → Yorumlar → "Reddet/Kaldır" kaydı siler; sipariş numarası yalnız o ürünü içeren gerçek siparişle eşleşirse yazılır. IP saklanmaz. Sipariş anonimleştirmesinde yorum kalır: ad kullanıcının yayımlanmasını istediği görünen addır, sipariş numarası anonim siparişe bağlanır
Sipariş sorgulama (D10): sipariş numarası + telefon | Hiçbir yere yazılmaz; yalnız istek anında `order` ile karşılaştırılır | Müşterinin siparişini bulması | Kalıcı değil | Deneme sayısı IP başına bellekte dakikalık sayılır (hız sınırı); istek logunda sorgu dizesi ve form gövdesi yoktur. Anonimleştirilmiş sipariş telefon maskeli olduğundan bulunamaz
Slug geçmişi (D10): kayıt türü, kayıt kimliği, eski adres | `slug_history` | Eski ürün/kategori adresinden 301 yönlendirme | Kaydın ömrü boyunca | Kişisel veri içermez
Kategori görseli (D10): görsel adresi ve dosyaları | `category.image_url`, `wwwroot/uploads/categories/{id}/` | Vitrin kapı kartı, başlık bandı, sekme | Yenisi yüklenene kadar | Kişisel veri içermez; yeni görsel yüklenince eski iki dosya silinir
Denetim izi: yönetici, işlem, kayıt, IP, zaman | `admin_audit_log` | Yönetim işlemlerinde hesap verebilirlik | 1 yıl | `AuditLogCleanupHostedService` gecede bir 365 günden eskiyi siler
Uygulama logu: yöntem, yol (sorgu dizesi yok), durum, süre, hata | `logs/heryerde-*.log` (konteynerde `/app/logs`) + konsol | İşletim ve hata ayıklama | 14 gün | Serilog `retainedFileCountLimit: 14`; `PiiMaskEnricher` telefon/e-posta/adresi maskeler — log PII'sız

## Aktarım
- İyzico (kartla ödeme): ad soyad, telefon, e-posta, adres, tutar, kart bilgisi — yalnız ödeme anında, sunucudan HTTPS ile.
- E-posta hizmet sağlayıcısı (`Notifications:Host` SMTP): alıcı e-postası ve posta gövdesi.
- Kargo şirketi: ad, telefon, adres (sistem dışı, yönetici eliyle).

## KVKK başvurusu
Kanal aydınlatma metnindedir (`[MÜŞTERİ: KVKK başvuru e-postası]`, KEP, yazılı başvuru). Başvuru en geç 30 gün içinde
yanıtlanır. Silme talebinde yasal saklama süresi dolmamışsa veri saklanır ve ilgili kişiye gerekçesi bildirilir;
saklama gerekmiyorsa sipariş kapandığında anonimleştirme aracı kullanılır; iletişim mesajı ve yorum yönetimden hemen silinir.

## Açık kalan
- Veritabanı yedekleme politikası tanımlı değil; yedek alınmaya başlanınca süre ve yer bu tabloya eklenmeli.
- Barındırma yeri kesinleşmedi (bkz. `docs/yasal-notlar.md`).
- İletişim mesajı saklama süresi (1 yıl) varsayılandır; müşteri farklı süre isterse `ContactMessageMaxAge` ve aydınlatma metni birlikte değişir.
