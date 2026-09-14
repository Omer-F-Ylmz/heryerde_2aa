# İthal-1 — brand_assets/raw ham fotoğrafları

`dotnet run --project HerYerde.Web -- --ithal brand_assets/raw` bu tabloyu okur. Her satır bir görsel;
aynı slug'ı taşıyan satırlar tek ürüne bağlanır. Fiyat bilinmediği için ürünler **price = 1** ve
**taslak** (`is_active = false`) olarak açılır; yönetici listesindeki "Fiyat eksik" süzgeci bunları toplar
ve fiyat girilmeden yayına alma 409 döner.

Ad, kategori ve açıklama görsellerden okunarak yazıldı; ölçü, malzeme ve parça sayısı fotoğrafta
görünenle sınırlı. "not" sütunu dolu olan ürünler **emin olunmayanlardır**, yayına almadan önce
doğrulanmalı.

| dosya | ad | slug | kategori | açıklama | parça | not |
| --- | --- | --- | --- | --- | --- | --- |
| Gemini_Generated_Image_3ro0943ro0943ro0.jpg | Katlanabilir Mutfak Seti 4'lü | katlanabilir-mutfak-seti-4-lu | saklama-duzenleme | Süzgeç, yıkama leğeni, süzgeçli kase ve huni. Silikon gövde katlanınca çekmeceye sığar; gri-beyaz ton. | 4 parça | Mutfak hazırlık gereci; "Saklama & Düzenleme" en yakın kategori olarak seçildi. |
| Gemini_Generated_Image_9ms91g9ms91g9ms9.jpg | Çelik Telli Peynir Kesme Tahtası | celik-telli-peynir-kesme-tahtasi | catal-kasik | Paslanmaz çelik gövde ve gerilebilir kesme teli. Peyniri ve tereyağını eşit kalınlıkta diliyor. | 1 parça | Sofra gereci; "Çatal-Kaşık" altına kondu, uygun değilse taşınmalı. |
| Gemini_Generated_Image_bx10qibx10qibx10.jpg | Hasır Örgü Saksı Sepeti | hasir-orgu-saksi-sepeti | sepet-dekor | Elde örülmüş doğal hasır saksı kılıfı. Konik gövde, mat doğal ton, boyasız. | 1 parça |  |
| Gemini_Generated_Image_hbpxl1hbpxl1hbpx.jpg | Altın İşlemeli Çatal Kaşık Takımı | altin-islemeli-catal-kasik-takimi | catal-kasik | Paslanmaz çelik gövde, sap ucunda altın rengi kabartma işleme. Yemek çatalı, tatlı çatalı, servis kaşığı, yemek kaşığı ve çay kaşığı. | 5 parça | Fotoğrafta tek takım var; kaç kişilik satıldığı görünmüyor. |
| Gemini_Generated_Image_up1a0jup1a0jup1a.jpg | Kristal Görünümlü LED Masa Lambası | kristal-gorunumlu-led-masa-lambasi | sepet-dekor | Altın rengi metal gövde, akrilik kristal başlık, sıcak beyaz LED. Yatak odası ve konsol için. | 1 parça | Fotoğrafta iki ayrı model yan yana; hangisinin satıldığı belirsiz. |
| Gemini_Generated_Image_ve9xqrve9xqrve9x.jpg | Pembe Kristal Desenli Cam Kase | pembe-kristal-desenli-cam-kase | yemek-takimi | Kalın cam gövde, kesme kristal deseni, pembe renk. Çerez ve sos için küçük kase. | 1 parça |  |
| Gemini_Generated_Image_ym4dy8ym4dy8ym4d.jpg | Gümüş Renkli Ayaklı Kadeh | gumus-renkli-ayakli-kadeh | yemek-takimi | Metalik gümüş kaplama cam, boğumlu ayak, geniş gövde. Su ve şerbet için. | 1 parça | Tek mi yoksa takım mı satıldığı fotoğraftan anlaşılmıyor. |
| Gemini_Generated_Image_ytwhjfytwhjfytwh.jpg | Kapaklı Hasır Desenli Piknik Sepeti | kapakli-hasir-desenli-piknik-sepeti | sepet-dekor | Gri hasır dokulu gövde, ortadan çift açılır kapak, katlanır taşıma sapı ve etiketlik. | 1 parça |  |
| Gemini_Generated_Image_z2ag4lz2ag4lz2ag.jpg | TAÇ Çelik Tencere Seti 3'lü | tac-celik-tencere-seti-3-lu | tencere-tava | Paslanmaz çelik gövde, temperli cam kapak, çift kulp. Üç farklı boyda tencere. | 3 parça | Fotoğrafta tek kapak görünüyor; her tencerenin kapaklı olup olmadığı doğrulanmalı. |

## Emin olunmayan ürünler

`katlanabilir-mutfak-seti-4-lu` · `celik-telli-peynir-kesme-tahtasi` · `altin-islemeli-catal-kasik-takimi` ·
`kristal-gorunumlu-led-masa-lambasi` · `gumus-renkli-ayakli-kadeh` · `tac-celik-tencere-seti-3-lu`
