# Ödeme ve teslimat

## Ödeme yöntemleri
`HerYerde.Entities.Enums.PaymentMethod`:

| Değer | Enum | Açılışta | Not |
|---|---|---|---|
| 1 | `KapidaOdeme` | Aktif | Kapıda nakit/kart. Instagram'dan gelen ilk alıcı için varsayılan güven yolu. |
| 2 | `HavaleEft` | Aktif | Sipariş sonrası IBAN paylaşılır; ödeme görülünce kargolanır. |
| 3 | `KrediKarti` | Hayır | Sanal POS anlaşması sonrası devreye alınacak. |

Ödeme adımında (`/odeme`) yalnız ilk iki yöntem seçilebilir; `KrediKarti` gelen istekte 400 döner.
Havale/EFT seçilince IBAN kutusu açılır (`Shop:Iban`).

## Teslimat
- Kargo ücreti **alıcıya** aittir: `appsettings` → `Shop:ShippingFee`, siparişe `order.shipping_fee`
  olarak kopyalanır (sonradan ücret değişse de eski sipariş değişmez). Toplam = ara toplam + kargo.
- Ücretsiz kargo eşiği ve kargo firması anlaşması henüz yok; kargo takip numarası D5'te.

## Sipariş durumu
`OrderStatus`: Beklemede → Onaylandi → Kargoda → TeslimEdildi. Geçiş tek yönlüdür, aşama atlanmaz
ve geri alınmaz; iptal yalnız **Beklemede** anında yapılır. Kural: `Business/Rules/OrderRules`.

## İletişim
- WhatsApp: `appsettings.json` → `Shop:WhatsApp` (`https://wa.me/905424970982`).
  Teşekkür sayfasındaki "siparişimi bildir" bağlantısı sipariş numarasını hazır metne koyar.
