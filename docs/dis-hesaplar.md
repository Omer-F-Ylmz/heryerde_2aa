# Dış hesaplar

Yayın öncesi dışarıdan alınıp yapılandırmaya yazılması gereken değerler. Gizli olanlar
`appsettings*.json` yerine ortam değişkenine yazılır (`:` yerine `__`). Tümü boşken uygulama açılır
ve testler geçer; eksik ayarın tek sonucu o özelliğin sessizce devre dışı kalmasıdır (bildirim
gönderimi "atlandı" olarak uyarı seviyesinde loglanır).

| Değer | Nereden alınır | Anahtar / ortam değişkeni | Yayın öncesi zorunlu mu |
|---|---|---|---|
| Veritabanı bağlantısı | Sunucu sağlayıcısı | `ConnectionStrings__Default` (env) | Evet |
| Yönetici e-postası ve parolası | [MÜŞTERİ] | `Admin__Email`, `Admin__Password` (env) | Evet |
| Yayın adresi (canonical, e-posta bağlantıları) | Alan adı kaydı | `Shop:BaseUrl` | Evet |
| SMTP sunucusu | E-posta/hosting sağlayıcısı | `Notifications:Host` | Evet |
| SMTP portu | E-posta sağlayıcısı | `Notifications:Port` | Hayır (varsayılan 587) |
| SMTP kullanıcı adı | E-posta sağlayıcısı | `Notifications__User` (env) | Evet |
| SMTP parolası | E-posta sağlayıcısı | `Notifications__Password` (env) | Evet |
| Gönderen e-posta adresi | [MÜŞTERİ] (mağaza alan adı) | `Notifications__From` (env) | Evet |
| "Yeni sipariş" bildiriminin gideceği adres | [MÜŞTERİ] | `Notifications__StoreTo` (env) | Evet |
| Kargo firmaları ve takip adresi şablonu | [MÜŞTERİ] kargo anlaşması | `Shipping:Carriers` (`Name`, `TrackingUrl`; `{0}` takip numarası) | Evet |
| Bedava kargo eşiği | [MÜŞTERİ] | `Shop:FreeShippingOver` (0 = eşik kapalı) | Evet (şu an geçici 2500) |
| Kargo ücreti | [MÜŞTERİ] kargo anlaşması | `Shop:ShippingFee` | Evet |
| Havale/EFT IBAN | [MÜŞTERİ] bankası | `Shop__Iban` (env) | Evet |
| WhatsApp sipariş numarası | [MÜŞTERİ] | `Shop:WhatsApp` | Evet |

## Notlar
- `Notifications:Host` ya da `Notifications__From` boşken gönderici "yapılandırılmamış" sayılır;
  kuyruktaki postalar silinmez, ayar geldiğinde ilk turda gönderilir.
- `Notifications__StoreTo` boşsa mağaza bildirimi hiç kuyruğa girmez; müşteri postası etkilenmez.
- `Shipping:Carriers` listesinde olmayan bir kargo firmasıyla "Kargoda" durumuna geçilemez (400).
- Takip adresi şablonu boş bırakılırsa teşekkür sayfasında bağlantı değil yalnız takip numarası çıkar.
