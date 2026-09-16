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
  ClosedXML.Parser 2.0.0 (MIT) ve SixLabors.Fonts 1.0.0 (**Apache-2.0**; nuspec `license expression`. Split License yalnız
  ImageSharp'a ve Fonts'un sonraki majör sürümlerine uygulanır).
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

## ZXing.Net 0.16.11

Kargo etiketindeki sipariş numarası Code128 barkodunu üretmek için (`HerYerde.Web/Infrastructure/OrderPdf.cs`); piksel verisi
ImageSharp ile PNG'ye çevrilir (System.Drawing yok).

- Lisans: **Apache-2.0**. Ticari kullanım serbest; NOTICE/lisans metninin korunması yeterli:
  <https://github.com/micjahn/ZXing.Net/blob/master/COPYING>

## PdfPig 0.1.16 (yalnız test)

Testlerde üretilen PDF'lerin metnini okumak için (`tests/HerYerde.Tests`). Lisans: **Apache-2.0**; üretime gitmez.

## Diğer doğrudan NuGet bağımlılıkları

Kanıt: paketin nuspec'indeki `license` alanı (`%USERPROFILE%\.nuget\packages\<ad>\<sürüm>\<ad>.nuspec`). Sürüm değişince bu tablo
güncellenir; `ReleaseGateTests` her `PackageReference` ad+sürümünün burada geçtiğini denetler.

| Paket | Sürüm | Proje | Lisans |
|---|---|---|---|
| Autofac | 9.3.2 | Business | MIT |
| Autofac.Extensions.DependencyInjection | 11.0.2 | Web | MIT |
| Microsoft.Extensions.Identity.Core | 10.0.11 | Business | MIT |
| Microsoft.EntityFrameworkCore | 10.0.11 | DataAccess | MIT |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.11 | DataAccess | MIT |
| Microsoft.EntityFrameworkCore.Design | 10.0.11 | Web (yalnız tasarım zamanı: migration) | MIT |
| MailKit | 4.18.0 | Web | MIT |
| Sentry.Serilog | 6.11.0 | Web | MIT |
| Serilog.AspNetCore | 10.0.0 | Web | Apache-2.0 |
| coverlet.collector | 6.0.4 | Tests (üretime gitmez) | MIT |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.11 | Tests (üretime gitmez) | MIT |
| Microsoft.NET.Test.Sdk | 17.14.1 | Tests (üretime gitmez) | MIT |
| PuppeteerSharp | 25.10.0 | Tests (üretime gitmez) | MIT |
| xunit | 2.9.3 | Tests (üretime gitmez) | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Tests (üretime gitmez) | Apache-2.0 |

## Geçişli NuGet bağımlılıkları

`dotnet list HerYerde.sln package --include-transitive` çıktısı (2026-09-16), lisansı nuspec'ten.

Çalışma zamanı:
- **MIT** (71): Azure.Core 1.50.0, Azure.Identity 1.17.1, BouncyCastle.Cryptography 2.7.0, ClosedXML.Parser 2.0.0, DocumentFormat.OpenXml 3.1.1, DocumentFormat.OpenXml.Framework 3.1.1, ExcelNumberFormat 1.1.0, Humanizer.Core 2.14.1, Microsoft.AspNetCore.Cryptography.Internal 10.0.11, Microsoft.AspNetCore.Cryptography.KeyDerivation 10.0.11, Microsoft.Bcl.AsyncInterfaces 8.0.0, Microsoft.Build.Framework 18.0.2, Microsoft.CodeAnalysis.Analyzers 3.11.0, Microsoft.CodeAnalysis.Common 5.0.0, Microsoft.CodeAnalysis.CSharp 5.0.0, Microsoft.CodeAnalysis.CSharp.Workspaces 5.0.0, Microsoft.CodeAnalysis.Workspaces.Common 5.0.0, Microsoft.CodeAnalysis.Workspaces.MSBuild 5.0.0, Microsoft.Data.SqlClient 6.1.6, Microsoft.EntityFrameworkCore.Abstractions 10.0.11, Microsoft.EntityFrameworkCore.Analyzers 10.0.11, Microsoft.EntityFrameworkCore.Relational 10.0.11, Microsoft.Extensions.Caching.Abstractions 10.0.11, Microsoft.Extensions.Caching.Memory 10.0.11, Microsoft.Extensions.Configuration 10.0.11, Microsoft.Extensions.Configuration.Abstractions 10.0.11, Microsoft.Extensions.Configuration.Binder 10.0.11, Microsoft.Extensions.DependencyInjection 10.0.11, Microsoft.Extensions.DependencyInjection.Abstractions 10.0.11, Microsoft.Extensions.DependencyModel 10.0.11, Microsoft.Extensions.Diagnostics 10.0.11, Microsoft.Extensions.Diagnostics.Abstractions 10.0.11, Microsoft.Extensions.Logging 10.0.11, Microsoft.Extensions.Logging.Abstractions 10.0.11, Microsoft.Extensions.Options 10.0.11, Microsoft.Extensions.Options.ConfigurationExtensions 10.0.11, Microsoft.Extensions.Primitives 10.0.11, Microsoft.Identity.Client 4.84.2, Microsoft.Identity.Client.Broker 4.84.2, Microsoft.Identity.Client.Extensions.Msal 4.78.0, Microsoft.IdentityModel.Abstractions 8.14.0, Microsoft.IdentityModel.JsonWebTokens 7.7.1, Microsoft.IdentityModel.Logging 7.7.1, Microsoft.IdentityModel.Protocols 7.7.1, Microsoft.IdentityModel.Protocols.OpenIdConnect 7.7.1, Microsoft.IdentityModel.Tokens 7.7.1, Microsoft.SqlServer.Server 1.0.0, Microsoft.VisualStudio.SolutionPersistence 1.0.52, Microsoft.Win32.SystemEvents 6.0.0, MimeKit 4.18.0, Mono.TextTemplating 3.0.0, Newtonsoft.Json 13.0.4, RBush.Signed 4.0.0, Sentry 6.11.0, System.ClientModel 1.8.0, System.CodeDom 6.0.0, System.Composition 9.0.0, System.Composition.AttributedModel 9.0.0, System.Composition.Convention 9.0.0, System.Composition.Hosting 9.0.0, System.Composition.Runtime 9.0.0, System.Composition.TypedParts 9.0.0, System.Configuration.ConfigurationManager 9.0.11, System.Diagnostics.EventLog 9.0.11, System.Drawing.Common 6.0.0, System.IdentityModel.Tokens.Jwt 7.7.1, System.IO.Packaging 8.0.1, System.Memory.Data 8.0.1, System.Security.Cryptography.Pkcs 10.0.0, System.Security.Cryptography.Pkcs 9.0.11, System.Security.Cryptography.ProtectedData 9.0.11
- **Apache-2.0** (9): Serilog 4.3.0, Serilog.Extensions.Hosting 10.0.0, Serilog.Extensions.Logging 10.0.0, Serilog.Formatting.Compact 3.0.0, Serilog.Settings.Configuration 10.0.0, Serilog.Sinks.Console 6.1.1, Serilog.Sinks.Debug 3.0.0, Serilog.Sinks.File 7.0.0, SixLabors.Fonts 1.0.0
- **Microsoft Software License Terms** (2): Microsoft.Data.SqlClient.SNI.runtime 6.0.2, Microsoft.Identity.Client.NativeInterop 0.20.6 — SqlClient'ın ve MSAL'ın
  yerel (native) bileşenleri; lisans dosyası pakette (`LICENSE.txt` / `LICENSE`). Açık kaynak değildir: "Distributable Code"
  olarak uygulamanın içinde dağıtılabilir, tek başına dağıtılamaz; tersine mühendislik yasaktır.

Yalnız test (üretime gitmez):
- **MIT** (24): Microsoft.AspNetCore.TestHost 10.0.11, Microsoft.CodeCoverage 17.14.1, Microsoft.Extensions.Configuration.CommandLine 10.0.11, Microsoft.Extensions.Configuration.EnvironmentVariables 10.0.11, Microsoft.Extensions.Configuration.FileExtensions 10.0.11, Microsoft.Extensions.Configuration.Json 10.0.11, Microsoft.Extensions.Configuration.UserSecrets 10.0.11, Microsoft.Extensions.FileProviders.Abstractions 10.0.11, Microsoft.Extensions.FileProviders.Physical 10.0.11, Microsoft.Extensions.FileSystemGlobbing 10.0.11, Microsoft.Extensions.Hosting 10.0.11, Microsoft.Extensions.Hosting.Abstractions 10.0.11, Microsoft.Extensions.Logging.Configuration 10.0.11, Microsoft.Extensions.Logging.Console 10.0.11, Microsoft.Extensions.Logging.Debug 10.0.11, Microsoft.Extensions.Logging.EventLog 10.0.11, Microsoft.Extensions.Logging.EventSource 10.0.11, Microsoft.IO.RecyclableMemoryStream 3.0.1, Microsoft.TestPlatform.ObjectModel 17.14.1, Microsoft.TestPlatform.TestHost 17.14.1, Newtonsoft.Json 13.0.3, ReactiveExtensionsSharp 0.3.0, System.Diagnostics.EventLog 10.0.11, WebDriverBiDi 0.0.54
- **Apache-2.0** (6): xunit.abstractions 2.0.3, xunit.analyzers 1.18.0, xunit.assert 2.9.3, xunit.core 2.9.3, xunit.extensibility.core 2.9.3, xunit.extensibility.execution 2.9.3
- Tarayıcı testleri (PuppeteerSharp) koşu sırasında Chrome for Testing indirir; tarayıcının kendi lisansı (Chromium BSD-3-Clause ve
  bileşenleri) yalnız CI/yerel test makinesini ilgilendirir, paket bağımlılığı değildir.

## npm: tailwindcss ve @tailwindcss/cli (yalnız CSS derlemesi)

`package.json` devDependencies; `src/input.css` → `wwwroot/css/site.css` derlemesinde kullanılır. Uygulamaya paket olarak gitmez,
yalnız üretilen `site.css` gider. `package-lock.json` ağacı (lisans alanından):
- **MIT** (49): @jridgewell/gen-mapping 0.3.13, @jridgewell/remapping 2.3.5, @jridgewell/resolve-uri 3.1.2, @jridgewell/sourcemap-codec 1.6.0, @jridgewell/trace-mapping 0.3.31, @parcel/watcher 2.5.1 (+13 platform ikilisi aynı sürüm), @tailwindcss/cli 4.3.3, @tailwindcss/node 4.3.3, @tailwindcss/oxide 4.3.3 (+12 platform ikilisi aynı sürüm), braces 3.0.3, enhanced-resolve 5.24.5, fill-range 7.1.1, is-extglob 2.1.1, is-glob 4.0.3, is-number 7.0.0, jiti 2.7.0, magic-string 0.30.21, micromatch 4.0.8, mri 1.2.0, node-addon-api 7.1.1, picomatch 2.3.2, tailwindcss 4.3.3, tapable 2.3.3, to-regex-range 5.0.1
- **MPL-2.0** (12): lightningcss 1.32.0 (+11 platform ikilisi aynı sürüm). Dosya düzeyinde copyleft: yalnız lightningcss'in kendi dosyaları
  değiştirilip dağıtılırsa kaynak açılır; burada değiştirilmeden derleme aracı olarak kullanılıyor, çıktısı (`site.css`) kapsam dışı.
- **ISC** (2): graceful-fs 4.2.11, picocolors 1.1.1
- **Apache-2.0** (2): detect-libc 1.0.3, detect-libc 2.1.2
- **BSD-3-Clause** (1): source-map-js 1.2.1

## Fontlar: Figtree ve Lora

`HerYerde.Web/wwwroot/fonts/` (figtree-latin, figtree-latin-ext, lora-latin, lora-latin-ext `.woff2`); kendi sunucumuzdan servis
edilir, üçüncü tarafa istek yok. Kanıt: woff2 `name` tablosu (telif ve lisans adresi) ile google/fonts deposundaki `OFL.txt`.

- **Figtree 2.002** — **SIL Open Font License 1.1**. "Copyright 2022 The Figtree Project Authors
  (https://github.com/erikdkennedy/figtree)". <https://github.com/google/fonts/tree/main/ofl/figtree>
- **Lora 3.008** — **SIL Open Font License 1.1**. "Copyright 2011 The Lora Project Authors (https://github.com/cyrealtype/Lora-Cyrillic),
  with Reserved Font Name "Lora"." <https://github.com/google/fonts/tree/main/ofl/lora>
- OFL: sitede servis edilmesi ve ticari kullanım serbest; yazı tipi tek başına satılamaz, değiştirilmiş sürüm ayrılmış adı ("Lora")
  taşıyamaz. Dosyalardaki telif ve lisans kayıtları (name tablosu) korunur.

## İl-ilçe verisi (HerYerde.Web/wwwroot/data/il-ilce.json)

Ödeme ve adres formlarındaki il → ilçe seçimi (81 il, 973 ilçe).

- Kaynak: **TÜİK** — Adrese Dayalı Nüfus Kayıt Sistemi (ADNKS) 31.12.2021 sonuçları, ilçelere göre il/ilçe merkezleri ile belde/köy
  nüfusları: <https://www.tuik.gov.tr/indir/duyuru/favori_raporlar.xlsx> (alınma 2026-09-16, SHA-256
  `3901a6d86382c61175b7e9ca06db8a2b1fb16ece662bd0fb5a947f13b503e7b4`). İl kodu ve adı ile ilçe adları dosyadan alındı; büyük harfli
  adlar Türkçe kurallarla (İ/I ayrımı kaynakta korunmuş) başlık biçimine çevrildi.
- Koşullar: TÜİK Yasal Uyarı — "İnternet sitemizden, yayınlarımızdan veya veri tabanlarımızdan elde edilen verilerin, kaynak
  gösterilmek suretiyle herhangi bir izine gerek duymaksızın yeniden kullanımı mümkündür."
  <https://www.tuik.gov.tr/Kurumsal/Yasal_Uyari>. Kaynak gösterimi dosyanın `kaynak` alanında ve bu notta.
- Önceki (kaynağı belirsiz) listeden tek fark: Kırıkkale "Bahşili" → "Bahşılı" (TÜİK ve NVİ yazımı).

## Docker imajları ve CI araçları

| Ad | Sürüm | Nerede | Lisans |
|---|---|---|---|
| node | 22-alpine | Dockerfile CSS aşaması (imaja girmez) | MIT (Node.js) + Alpine paketleri |
| mcr.microsoft.com/dotnet/sdk | 10.0 | Dockerfile derleme aşaması (imaja girmez) | MIT (.NET) |
| mcr.microsoft.com/dotnet/aspnet | 10.0 | Çalışan imajın tabanı | MIT (.NET) + Ubuntu paketleri |
| mcr.microsoft.com/mssql/server | 2022-latest | docker-compose.prod.yml `db`, CI | **Microsoft SQL Server EULA** (açık kaynak değil) |
| actions/checkout, setup-dotnet, setup-node, upload-artifact | v4 | CI | MIT |
| rhysd/actionlint | 1.7.7 | CI (workflow lint) | MIT |
| ghcr.io/zaproxy/zaproxy | stable | CI (zap-scan) | Apache-2.0 |

- **SQL Server sürümü:** compose `MSSQL_PID` vermez; imaj bu durumda **Developer Edition** açar ve Developer Edition yalnız
  geliştirme/test için lisanslıdır, canlıda kullanılamaz. **[MÜŞTERİ]:** canlı sunucuda ücretsiz Express (`MSSQL_PID=Express`;
  veritabanı başına 10 GB, 1 soket/4 çekirdek, 1410 MB tampon sınırı) ya da lisanslı Standard/Enterprise seçilmeli
  (<https://learn.microsoft.com/sql/linux/sql-server-linux-configure-environment-variables>). CI ve staging Developer Edition'da kalabilir.
