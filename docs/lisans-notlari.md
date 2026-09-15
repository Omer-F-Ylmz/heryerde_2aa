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

## ClosedXML 0.105.1

Toplu ürün tablosunu (.xlsx) yazmak ve okumak için kullanılıyor (`HerYerde.Web/Infrastructure/ProductSheet.cs`).

- Lisans: **MIT** — <https://github.com/ClosedXML/ClosedXML/blob/develop/LICENSE>
- Geçişli bağımlılıklar: DocumentFormat.OpenXml 3.1.1 (MIT), ExcelNumberFormat 1.1.0 (MIT), RBush.Signed 4.0.0 (MIT),
  ClosedXML.Parser 2.0.0 (MIT) ve **SixLabors.Fonts 1.0.0** (Six Labors Split License 1.0 — ImageSharp ile aynı koşul:
  eşik üstü ticari gelirde ücretli lisans; 1.x derlemede anahtar istemez).
- Yükleme yolu kütüphaneye güvenmiyor: dosya önce zip imzasıyla (`PK\x03\x04`) ve 8 MB sınırıyla süzülür, 5000 satırı
  aşan tablo satırları okunmadan reddedilir; açma hatası 400'e çevrilir.

## QRCoder 1.8.0

Yönetici iki adımlı doğrulama kurulumunda `otpauth://` adresinin QR kodunu PNG olarak üretmek için kullanılıyor
(`HerYerde.Web/Areas/Admin/Controllers/AuthController.cs`). Görsel sayfaya `data:` adresiyle gömülür; dış servis yok.

- Lisans: **MIT**. Ticari kullanım serbest, bildirim metninin korunması yeterli:
  <https://github.com/codebude/QRCoder/blob/master/LICENSE.txt>
- Yalnız `QRCodeGenerator` + `PngByteQRCode` kullanılıyor (System.Drawing bağımlılığı olmayan yol; Linux imajında çalışır).

## QuestPDF 2026.9.0

Sözleşme arşivi (ön bilgilendirme + mesafeli satış sözleşmesi) ve cayma formu PDF'lerini üretmek için kullanılıyor
(`HerYerde.Web/Infrastructure/LegalPdf.cs`). Varsayılan yazı tipi paketle gelen Lato (SIL Open Font License 1.1).

- Lisans: **QuestPDF Community License** (MIT/Apache değil). Koşullar: yıllık brüt geliri **1.000.000 USD altında** olan
  şirket ve bireyler ile kâr amacı gütmeyen, akademik ve açık kaynak projeler ücretsiz ve ticari amaçla kullanabilir; halka açık
  şirketler ve kamu kurumları gelirden bağımsız olarak kapsam dışıdır. Eşik aşılırsa 90 gün içinde Professional/Enterprise
  lisansa geçilir. Kod lisans türünü açıkça bildirir (`QuestPDF.Settings.License = LicenseType.Community`).
  <https://www.questpdf.com/license/community.html>
- **[MÜŞTERİ]:** işletmenin yıllık brüt gelirinin eşiğin altında olduğu teyit edilmeli; eşik aşılırsa lisans satın alınır.

## PdfPig 0.1.16 (yalnız test)

Testlerde üretilen PDF'lerin metnini okumak için (`tests/HerYerde.Tests`). Lisans: **Apache-2.0**; üretime gitmez.
