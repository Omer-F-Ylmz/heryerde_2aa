# Üçüncü taraf lisans notları

## SixLabors.ImageSharp 3.1.12

Ürün görsellerini kareye oturtup 400/800/1200 px webp üretmek için kullanılıyor
(`HerYerde.Web/Infrastructure/ProductImageStorage.cs`).

- Lisans: **Six Labors Split License 1.0**. Açık kaynak/küçük ölçekli kullanım ücretsizdir; yıllık geliri
  eşiğin üstündeki ticari kullanım ücretli lisans ister. Koşullar:
  <https://github.com/SixLabors/ImageSharp/blob/main/LICENSE>
- **Sürüm neden sabit:** ImageSharp 4.x derleme sırasında `SixLaborsLicenseKey` istiyor ve anahtarsız
  derleme hata veriyor. 3.1.12, anahtar gerektirmeyen son sürüm. Ticari lisans alındığında 4.x'e
  geçilebilir; API (`ResizeOptions` + `WebpEncoder`) aynı kaldığı için değişiklik yalnız sürüm satırıdır.
- Yükleme yolu ImageSharp'ın çözücüsüne güvenmiyor: dosya türü `ImageFile.Kind` ile sihirli baytlardan
  okunuyor, 8 MB üstü içerik çözücüye hiç verilmiyor.
