# Kişisel veri saklama ve imha politikası (D13) [ÖNERİ]

Dayanak: 6698 sayılı KVKK m. 7, Kişisel Verilerin Silinmesi, Yok Edilmesi veya Anonim Hale Getirilmesi Hakkında Yönetmelik. Hangi verinin
nerede durduğu `docs/veri-envanteri.md`'de; bu belge o verilerin ne zaman ve nasıl imha edildiğini ve kimin sorumlu olduğunu yazar.
Hukukçu teyidi `docs/yasal-notlar.md` sürecindedir.

## Sorumlular
- Veri sorumlusu: [MÜŞTERİ: ticari unvan]. Politika sahibi ve KVKK başvurularına yanıt veren kişi: [MÜŞTERİ: ad, unvan].
- Teknik uygulayıcı: yönetim paneli yöneticisi (anonimleştirme, silme, KVKK aracı) ve sunucu yöneticisi (yedek, log).

## İmha yöntemleri
- **Silme:** kayıt veritabanından silinir (`DELETE`); dosya gizli depodan (`/app/private`) silinir.
- **Anonimleştirme:** kişiyi belirleyen alanlar maskelenir (ad `A*** Y***`, telefon `05*******82`, e-posta `a***@***`, adres `***`),
  serbest metin ve IBAN silinir; tutar, tarih, ürün ve il/ilçe istatistik ve yasal saklama için kalır. Geri döndürülemez.
- **Yok etme:** yedek dosyaları süresi dolunca üzerine yazılarak/silinerek kaldırılır; fiziksel ortam kullanılmaz.

## Saklama ve imha tablosu
Veri | Kayıt | Saklama | İmha | Yöntem
---|---|---|---|---
Sipariş kişisel verisi, fatura | `order`, `/app/private/faturalar/` | 10 yıl (6502, TTK m. 82, VUK m. 253) | Süre sonunda ya da talep üzerine sipariş kapandığında | Anonimleştirme (fatura yasal belge olarak kalır)
Havale bildirimi ve dekont | `payment_notice`, `/app/private/dekontlar/` | Siparişle birlikte | Anonimleştirmede | Ad maskelenir, dekont silinir
İade/değişim talebi: neden, IBAN, fotoğraf | `return_request`, `return_request_item`, `/app/private/iadeler/` | Siparişle birlikte (cayma ve iade ispatı) | Anonimleştirmede | Neden ve IBAN silinir, fotoğraf silinir; tür, durum, tutar kalır
Müşteri iptalinde geri ödeme IBAN'ı | `order.refund_iban` | Siparişle birlikte | Anonimleştirmede | Silme
Sözleşme arşivi (sürüm başına ön bilgilendirme + mesafeli satış PDF'i) | `/app/private/sozlesmeler/` | Süresiz | — | Kişisel veri içermez
E-posta bildirimleri | `outbox_message` | Gönderimden 30 gün | Gecelik `PersonalDataCleanupHostedService` | Silme
İletişim mesajı | `contact_message` | 1 yıl | Gecelik `PersonalDataCleanupHostedService`; yönetimden hemen | Silme
KVKK başvuru kaydı | `kvkk_request` | Yanıttan sonra 3 yıl (ispat) | Periyodik imhada | Silme
Sepet | `cart`, `cart_item` | 30 gün | Gecelik `CartCleanupHostedService` | Silme
Denetim izi | `admin_audit_log` | 1 yıl | Gecelik `AuditLogCleanupHostedService`; sipariş anonimleştirmesinde ayrıntı hemen | Silme
Uygulama logu | `logs/heryerde-*.log` | 14 gün (PII maskeli) | Serilog dosya sınırı | Silme
Yedekler | `backups/` | 14 gün | Gecelik yedekte en eskisi | Yok etme

## Periyodik imha
- Otomatik işler günlük çalışır (e-posta, iletişim mesajı, sepet, denetim izi, log, yedek).
- Elle yapılan periyodik imha **6 ayda bir** (Yönetmelik m. 11: en çok 6 ay aralıkla): yönetici saklama süresi dolan kapanmış
  siparişleri ve 3 yılı geçen KVKK başvuru kayıtlarını gözden geçirip anonimleştirir/siler; yapılan her işlem denetim izine yazılır.
  Bir sonraki imha tarihi: [MÜŞTERİ: tarih].

## Talep üzerine imha
- İlgili kişi başvurusu `/admin/kvkk` aracıyla (telefon ya da e-postayla kişi bulunur, erişim dökümü ya da anonimleştirme) en geç
  30 gün içinde sonuçlandırılır; araç başvuru tarihinden itibaren kalan günü gösterir.
- Yasal saklama süresi dolmamış veri (açık sipariş, fatura) silinmez; başvurana gerekçesi bildirilir.
- Geri yüklenen yedekte son 14 gün içindeki anonimleştirmeler yeniden yapılır (bkz. `docs/yedekleme.md`).
