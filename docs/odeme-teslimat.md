# Ödeme ve teslimat

## Ödeme yöntemleri
`HerYerde.Entities.Enums.PaymentMethod`:

| Değer | Enum | Açılışta | Not |
|---|---|---|---|
| 1 | `KapidaOdeme` | Aktif | Kapıda nakit/kart. Instagram'dan gelen ilk alıcı için varsayılan güven yolu. |
| 2 | `HavaleEft` | Aktif | Sipariş sonrası IBAN paylaşılır; ödeme görülünce kargolanır. |
| 3 | `KrediKarti` | Anahtar varsa | İyzico 3D Secure (D7). `Iyzico:ApiKey`/`SecretKey` boşken seçenek görünmez, POST 400. |

Havale/EFT seçilince IBAN kutusu açılır (`Shop:Iban`).

### Kartla ödeme (İyzico 3D Secure)
1. `POST /odeme` kartla: sipariş `Beklemede`, `payment` satırı `Baslatildi` açılır. Stok düşmez, sepet
   silinmez, müşteri postası gitmez (mağaza postası sipariş anında gider). Sağlayıcıya ödeme + 3DS
   başlatma isteği atılır; dönen form bizim sayfamızda kurulup `site.js` ile bankaya gönderilir.
2. `POST /odeme/3d-donus` (antiforgery yok, dakikada 30, GET 405): `conversation_id` ile eşleşir,
   imza ve `mdStatus` doğrulanır. Kayıt tek koşullu UPDATE ile kapatılır; ikinci dönüş ilk sonucu alır.
3. Aynı işlemde stok düşer, çekim yapılır (`/payment/3dsecure/auth`), çekim tutarı sipariş toplamıyla
   birebir karşılaştırılır. Başarıda sepet silinir, müşteri postası kuyruğa girer, teşekkür sayfasına 303.
4. Reddedilen/imzası tutmayan/tutarı uyuşmayan dönüşte işlem geri alınır: sipariş `IptalEdildi`,
   ödeme `Basarisiz`, stok değişmez; `/odeme`'ye mesajla 303, sepet yerinde kalır.
- Kart bilgisi yalnız istek belleğinde sağlayıcıya iletilir; `payment.raw_response` 4000 karakterde
  sığmazsa üst düzey dizi/nesneleri atılır (geçerli JSON kalır); yalnız `cardNumber`/`binNumber` (son 4 hane hariç),
  `cvc`, `cardHolderName` alanları maskelenir, 3DS HTML'i saklanmaz. İmzasız başarılı init/auth yanıtı reddedilir.
- CSP `form-action` ve `frame-src` yalnız `/odeme*` yollarında `Iyzico:CspSources` kökenlerini alır.
- Dönüş adresi `Shop:BaseUrl`'den kurulur (Host başlığından değil). İade (D8) ve taksit yok.

## Teslimat
- Kargo ücreti **alıcıya** aittir: `appsettings` → `Shop:ShippingFee`, siparişe `order.shipping_fee`
  olarak kopyalanır (sonradan ücret değişse de eski sipariş değişmez). Toplam = ara toplam + kargo.
- Ücretsiz kargo eşiği `Shop:FreeShippingOver`: ara toplam eşiğe ulaşınca kargo 0 ₺ yazılır (0 = eşik
  kapalı). Sepet ve ödeme özetinde eşiğe kalan tutar ilerleme çubuğuyla gösterilir.
- Kargo firması ve takip numarası siparişte tutulur (`order.carrier`, `order.tracking_no`);
  "Kargoda" durumuna geçerken ikisi de zorunludur. Firma listesi ve takip adresi şablonu
  `Shipping:Carriers` altında; bkz. [dis-hesaplar.md](dis-hesaplar.md).

## Sipariş durumu
`OrderStatus`: Beklemede → Onaylandi → Kargoda → TeslimEdildi. Geçiş tek yönlüdür, aşama atlanmaz
ve geri alınmaz; iptal yalnız **Beklemede** anında yapılır. Kural: `Business/Rules/OrderRules`.

## İletişim
- WhatsApp: `appsettings.json` → `Shop:WhatsApp` (`https://wa.me/905424970982`).
  Teşekkür sayfasındaki "siparişimi bildir" bağlantısı sipariş numarasını hazır metne koyar.
