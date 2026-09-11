# Veri envanteri (KAPANIŞ-5)

Hangi kişisel veri nerede, ne kadar tutulur ve nasıl silinir. Metinler: `/yasal/kvkk-aydinlatma`, `/yasal/gizlilik-politikasi`,
`/yasal/cerez-politikasi`.

Veri | Nerede | Amaç | Süre | Silme / anonimleştirme
---|---|---|---|---
Sipariş kişisel verisi: ad soyad, cep telefonu, e-posta, adres, il/ilçe, sipariş notu | `order` tablosu | Sözleşmenin kurulması ve ifası, teslimat, yasal saklama | 10 yıl (6502 / VUK) | Yönetim → sipariş detayı → "Kişisel veriyi anonimleştir"; yalnız Teslim edildi / İptal edildi siparişte. Ad `A*** Y***`, telefon `05*******82`, e-posta `a***@***`, adres `***`, not silinir; il/ilçe ve tutarlar kalır; denetim izi yazılır
Onay kaydı: `consent_at`, `legal_version` | `order` tablosu | Ön bilgilendirme ve mesafeli satış onayının ispatı | Siparişle birlikte | Anonimleştirmede korunur (kişisel veri değil)
Sipariş satırları: ürün adı, stok kodu, adet, fiyat | `order_item` | Sözleşme, fatura, stok | Siparişle birlikte | Kişisel veri içermez
Anonim sepet ve satırları | `cart`, `cart_item`; kimlik `heryerde.cart` çerezinde | Alışveriş sepeti | 30 gün | `CartCleanupHostedService` gecede bir siler
Son sipariş anahtarı | `heryerde.lastorder` çerezi | Teşekkür sayfasının bağlantısız açılması | 30 gün | Tarayıcıda süresi dolar
CSRF anahtarı | `.AspNetCore.Antiforgery.*` çerezi | Form güvenliği | Tarayıcı oturumu | Tarayıcı kapanınca
Yönetici hesabı: e-posta, parola özeti, hatalı giriş/kilit | `admin_user` | Yönetim paneline erişim | Hesap süresince | Elle
Yönetici oturumu | `heryerde.admin` çerezi | Yönetim oturumu | 8 saat (kayar) | Çıkışta / süre dolunca
Denetim izi: yönetici, işlem, kayıt, IP, zaman | `admin_audit_log` | Yönetim işlemlerinde hesap verebilirlik | 1 yıl | `AuditLogCleanupHostedService` gecede bir 365 günden eskiyi siler
Uygulama logu: yöntem, yol (sorgu dizesi yok), durum, süre, hata | `logs/heryerde-*.log` (konteynerde `/app/logs`) + konsol | İşletim ve hata ayıklama | 14 gün | Serilog `retainedFileCountLimit: 14`; `PiiMaskEnricher` telefon/e-posta/adresi maskeler — log PII'sız

## KVKK başvurusu
Kanal aydınlatma metnindedir (`[MÜŞTERİ: KVKK başvuru e-postası]`, KEP, yazılı başvuru). Başvuru en geç 30 gün içinde
yanıtlanır. Silme talebinde yasal saklama süresi dolmamışsa veri saklanır ve ilgili kişiye gerekçesi bildirilir;
saklama gerekmiyorsa sipariş kapandığında anonimleştirme aracı kullanılır.

## Açık kalan
- Veritabanı yedekleme politikası tanımlı değil; yedek alınmaya başlanınca süre ve yer bu tabloya eklenmeli.
- Barındırma yeri kesinleşmedi (bkz. `docs/yasal-notlar.md`).
