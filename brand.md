# HerYerde — marka kimliği

> Site adı **[ÖMER KARARI]** — geçici olarak "HerYerde" kullanılıyor. Ad değişirse wordmark,
> `<title>`, meta description ve `_Layout.cshtml` üzerinden tek noktadan güncellenir.

Hedef kitle: Instagram'dan gelen ev hanımı / çeyiz alıcısı. Ton: samimi, güven veren, kampanya
görünür ama bağırmayan. Karar tek satır:

**ana #A8442A · nötr #F6F1E8 · vurgu #0F6E5C · display Lora · sans Figtree · ölçek 8px**

## Palet [ÖNERİ]
| Rol | Hex | Nerede |
|---|---|---|
| Ana (kiremit) | `#A8442A` | Wordmark hover, birincil buton, kampanya rozeti |
| Ana koyu | `#5E2214` | Başlıklar (h1/h2/h3), wordmark |
| Ana orta | `#8A361F` | Panel etiketi, ikincil vurgu |
| Ana açık | `#F2DED6` | Rozet zemini |
| Nötr (krem) | `#F6F1E8` | Sayfa zemini |
| Nötr açık | `#FDFAF5` | Yükseltilmiş yüzey (kart) |
| Nötr çizgi | `#E5DBCB` | Ayraç, kart kenarı |
| Nötr metin | `#6B5B4E` | İkincil metin |
| Mürekkep | `#2B2118` | Gövde metni |
| Vurgu (yeşil) | `#0F6E5C` | İkon, focus halkası, güven mesajı |
| Vurgu koyu | `#0A4C40` | Eyebrow, "kapıda ödeme" notu |

Neden: kiremit + krem çeyiz/ev dokusunu (toprak, keten, bakır) taşır; yeşil vurgu hem güven hem
WhatsApp yeşiliyle uyum verir, kampanya kırmızısıyla yarışmaz. Mor/indigo yok.

Kontrast (hesaplanmış): `#2B2118` / krem ≈ 13:1 · `#6B5B4E` / krem ≈ 5.8:1 ·
`#A8442A` / krem ≈ 5.3:1 · `#0F6E5C` / krem ≈ 5.6:1 · beyaz / `#A8442A` ≈ 5.9:1. Hepsi ≥4.5:1.

## Tipografi [ÖNERİ]
- Display: **Lora** 600 — sıcak, okunaklı serif; Türkçe diakritikleri tam.
- Gövde: **Figtree** 400/600 — yumuşak geometrik sans, ekranda samimi.
- 2 aile / 3 dosya, `display=swap`. Ölçek: 14 / 16 / 20 / 32 / 48+ (h1 mobil 48 → 1440'ta 72).
- Büyük başlıkta `letter-spacing: -0.03em`, gövdede `line-height: 1.7`.

## Ölçek ve yüzey
- Boşluk yalnız 8 / 16 / 24 / 32 / 48 / 64 / 96.
- Üç yüzey: `surface-base` (krem + 2 radial gradient + SVG noise) → `surface-elevated` (kart,
  katmanlı kiremit gölge) → floating (D2 admin rail'i için ayrılmış).
- Animasyon yalnız `transform`/`opacity`, easing `cubic-bezier(.22,1,.36,1)`,
  `prefers-reduced-motion` ile kapanır.

## Metin [ÖNERİ]
D1 vitrin metinleri (h1 "Şalvardan sofraya, evin her yeri.", alt metin, iki alan paneli) öneridir;
onaylanmazsa değişir. Uydurma sayı, yorum veya müşteri logosu yok.
