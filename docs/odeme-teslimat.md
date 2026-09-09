# Ödeme ve teslimat (D1: yalnız karar, akış D4'te)

## Ödeme yöntemleri
`HerYerde.Entities.Enums.PaymentMethod`:

| Değer | Enum | Açılışta | Not |
|---|---|---|---|
| 1 | `KapidaOdeme` | Aktif | Kapıda nakit/kart. Instagram'dan gelen ilk alıcı için varsayılan güven yolu. |
| 2 | `HavaleEft` | Aktif | Sipariş sonrası IBAN paylaşılır; ödeme görülünce kargolanır. |
| 3 | `KrediKarti` | Hayır | Sanal POS anlaşması sonrası devreye alınacak. |

Sipariş kaydı, ödeme durumu ve doğrulama D4'te. D1'de yalnız bu enum vardır; UI'da seçim yoktur.

## Teslimat
- Kargo ücreti **alıcıya** aittir. Sipariş üzerinde `Order.shipping_fee` olarak D4'te tutulacak;
  D1'de sipariş varlığı yoktur.
- Ücretsiz kargo eşiği, kargo firması ve teslim süresi kararı D4'e bırakıldı.

## İletişim
- WhatsApp: `appsettings.json` → `Shop:WhatsApp` (`https://wa.me/905424970982`).
  Vitrindeki buton D3'te eklenecek; D1'de yalnız konfigürasyon değeri durur.
