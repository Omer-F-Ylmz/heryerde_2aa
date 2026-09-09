# UX kuralları — vitrin (ui-ux-pro-max'tan süzüldü, brand.md paleti sabit)
1. Kart hiyerarşisi: görsel → ad → fiyat → rozet; kart başına TEK mesaj, eşit boyutlu kart tekrarı yalnız ürün gridinde.
2. Fiyat: güncel fiyat kiremit ve kalın, eski fiyat üstü çizili nötr-700 ve küçük; `tabular-nums`; TL biçimi tr-TR ("1.290,50 ₺").
3. Kampanya: rozet metin taşır (renk tek başına anlam taşımaz); süresi dolan kampanya rozet ve indirimli fiyat düşer.
4. Dokunma hedefi: her `a/button/input/çip` ≥44×44px, komşu hedefler arası ≥8px.
5. Tek birincil CTA per ekran: yeşil "WhatsApp'tan sipariş ver"; ikincil kiremit çerçeve; pasif "Sepete ekle" `disabled` + opaklık 0.5 + tooltip.
6. Kontrast: gövde ≥4.5:1, büyük başlık ≥3:1; ikon/çerçeve ≥3:1; kampanya beyaz/kiremit 5.9:1.
7. Odak: her etkileşimde `focus-visible` 2px yeşil halka; hover'a bağımlı bilgi yok (2. görsel yalnız süs).
8. Görsel: her `img` width/height + alt; fold altı `loading="lazy"`; görsel yokken keten/kiremit SVG pattern (CLS 0).
9. Grid: 390 → 2 kolon, 768 → 3, 1024+ → 4; yatay kaydırma yok; konteyner max 1200px.
10. Tipografi: gövde 16px / 1.7; ölçek 14-16-20-32-48; başlık Lora, gövde Figtree; başlık `text-wrap: balance`.
11. Hareket: yalnız transform/opacity, 240ms `cubic-bezier(.22,1,.36,1)`; `prefers-reduced-motion` ile kapanır; sayfa başına en fazla 2 animasyon.
12. Sayım: countdown `tabular-nums`, `aria-live="polite"` yok (her saniye bağırmaz); bitince tamamen gizlenir.
13. Navigasyon: mevcut sekme görünür (`aria-current="page"`); sayfalama link, `rel=prev/next`; "yakında" kapı `<a>` DEĞİL, `aria-disabled`.
14. Varyant çipleri: `<fieldset><legend>` + radio; stok 0 → `disabled` + üstü çizili + "tükendi" metni.
15. İkon: tek aile Lucide outline 2px, SVG inline; emoji yok; ikon-yalnız düğmede `aria-label`.
