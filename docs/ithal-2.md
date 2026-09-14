# İthal-2 — Örtü & Eşarp ham fotoğrafları

`dotnet run --project HerYerde.Web -- --ithal brand_assets/raw --tablo docs/ithal-2.md` bu tabloyu okur.
`--tablo` verilmezse komut `docs/ithal-1.md` (Ev) tablosunu okur. Sütunlar ve kurallar İthal-1 ile aynı:
her satır bir görsel, aynı slug'lı satırlar tek ürüne bağlanır, ürün **price = 1** ve **taslak** açılır.

Farkı: kategori sütunu Örtü & Eşarp alt kategorilerinden biridir (veritabanı kökü `giyim`). Giyim kökündeki
ürün varyantsız yayına alınamaz; ithalden sonra yönetimde her ürüne en az bir renk/desen varyantı (beden boş
kalabilir) ve gerekiyorsa ölçü (ör. "70x70 cm") girilir.

| kategori slug'ı | vitrin adı |
| --- | --- |
| `esarp` | Eşarp |
| `basortusu` | Başörtüsü |
| `sal` | Şal |
| `namaz-ortusu` | Namaz Örtüsü |
| `bone-aksesuar` | Bone & Aksesuar |

## Ürün tablosu

Ürünler ve fotoğraflar [MÜŞTERİ] listesiyle gelecek; o zamana kadar tablo boş, bölüm vitrinde "Şu an ürün yok"
boş durumunu gösterir.

| dosya | ad | slug | kategori | açıklama | renk / desen | not |
| --- | --- | --- | --- | --- | --- | --- |
