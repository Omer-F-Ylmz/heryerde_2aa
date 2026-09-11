# Yasal metinler — notlar (KAPANIŞ-5)

`/yasal/{slug}` altındaki altı metin **şablondur**. Hukuki onay müşteri ve avukatı tarafındadır; yayından önce
okunup onaylanmalıdır. Bu not sayfalarda yoktur, yalnız burada durur.

## [MÜŞTERİ] ile işaretli boşluklar
Sayfalarda `[MÜŞTERİ: …]` biçiminde görünür (`Views/Legal/_Satici.cshtml` ve KVKK başvuru bölümü):
- ticari unvan
- açık adres
- MERSİS numarası
- vergi dairesi ve vergi numarası
- e-posta adresi
- telefon
- KEP adresi
- KVKK başvuru e-postası

## Onayda ayrıca bakılacak maddeler
- **İade kargo bedeli.** Teslimat kargosu alıcıya aittir; cayma hakkıyla iadede metin, satıcının WhatsApp'tan
  bildirdiği kargo firmasıyla gönderilen iadenin bedelini satıcıya yükler (Mesafeli Sözleşmeler Yönetmeliği).
  İş modeli farklıysa metin ve ön bilgilendirme birlikte değişmeli.
- **Barındırma yeri / yurt dışı aktarım.** Sunucunun yeri kesinleşmedi; KVKK aydınlatma metni yurt dışına aktarım
  hakkında hüküm kurmuyor. Barındırma seçilince aydınlatma ve gizlilik metni tamamlanmalı.
- **Teslim süresi.** Metin yasal üst sınırı (30 gün) yazar; daha kısa bir taahhüt verilecekse eklenir.
- **Uyuşmazlık parasal sınırları** her yıl ilan edilir; metin sabit tutar yazmaz.

## Sürüm
`HerYerde.Business.LegalDocs.Version` (şu an `2026-09-11`) ve `UpdatedAt` sayfalardaki "Son güncelleme" tarihini ve
siparişin `legal_version` alanını belirler. Metin değişince ikisi birlikte ilerletilir; eski sipariş onayladığı
sürümü saklar, o sürümün metni git geçmişinden okunur.

## Çerez onay bandı
Yalnız zorunlu çerezler kullanılır (sepet, antiforgery, son sipariş, yönetici oturumu); üçüncü taraf, analitik ya da
izleme çerezi yoktur. Bu yüzden onay bandı gösterilmez ve çerez politikası bunu açıkça söyler. Analitik/pazarlama
aracı eklenirse bant ve politika birlikte gelir.
