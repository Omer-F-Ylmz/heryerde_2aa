# Yedekleme ve geri dönüş

Barındırma kararından bağımsızdır: prod `docker-compose.prod.yml` ile kalktığı sürece aynı adımlar geçerlidir.

## Ne yedeklenir
| Parça | Dosya | Nasıl |
|---|---|---|
| Veritabanı | `backups/heryerde-yyyyMMdd-HHmmss.bak` | SQL Server `BACKUP DATABASE … WITH COPY_ONLY, CHECKSUM` |
| Yüklenen ürün görselleri | `backups/uploads-yyyyMMdd-HHmmss.tar.gz` | `heryerde-uploads` volume'unun (`/app/wwwroot/uploads`) arşivi, `.ithal.json` dahil |
| Fatura PDF'leri ve havale dekontları | `backups/belgeler-yyyyMMdd-HHmmss.tar.gz` | `heryerde-private` volume'unun (`/app/private`, wwwroot dışı) arşivi; kişisel veri içerir |

Saat damgası UTC'dir. Her türden **en yeni 14 dosya** kalır (gecelik yedekte 14 gün); 15.'si yedek alınırken silinir.
Loglar (`/app/logs`, 14 gün) ve kod yedeklenmez: kod git'te, imaj her an yeniden üretilir.

## Kurulum (bir kez)
```sh
# SQL Server (uid 10001) .bak yazar, uygulama kullanıcısı (uid 1654) arşiv yazar ve eskileri siler.
sudo install -d -m 0770 -o 10001 -g 1654 backups
docker compose -f docker-compose.prod.yml up -d
```
`backup` servisi her gün 00:00 UTC'de (03:00 İstanbul) yedek alır. Bağlantı dizesindeki kullanıcı `sa` değilse
yedek için `db_backupoperator`, prova geri yüklemesi için `dbcreator` rolü gerekir.

## Elle yedek almak
Migration'lı bir sürüm yayınlamadan önce mutlaka:
```sh
docker compose -f docker-compose.prod.yml run --rm backup "dotnet HerYerde.Web.dll --yedek-al /backups"
# Yedek: /backups/heryerde-20260915-000000.bak, arşiv: /backups/uploads-20260915-000000.tar.gz, belgeler: /backups/belgeler-20260915-000000.tar.gz, silinen: 0.
```
Komut uygulamayı açmaz, geçiş (migration) uygulamaz; yalnız yedek alıp çıkar.

## Geri dönüş
1. **Provaya aç** — canlı veritabanına dokunmaz, `HerYerde_prova` adına yükler (varsa üzerine yazar):
   ```sh
   docker compose -f docker-compose.prod.yml run --rm backup "dotnet HerYerde.Web.dll --yedek-yukle /backups/heryerde-20260915-000000.bak"
   # Geri yükleme: HerYerde_prova, 42 ürün.
   ```
2. **Doğrula** — ürün sayısı beklenene uyuyor mu; prova veritabanıyla ikinci bir uygulama açıp hazır mı:
   ```sh
   docker compose -f docker-compose.prod.yml run -d --name heryerde-prova -p 8081:8080 \
     -e "ConnectionStrings__Default=Server=db;Database=HerYerde_prova;User Id=…;Password=…;TrustServerCertificate=True" web
   curl -fsS http://localhost:8081/health/ready   # Healthy
   docker rm -f heryerde-prova
   ```
3. **Canlıya geç** — `web`'i durdur, `HERYERDE_CONNECTION_STRING` içinde `Database=HerYerde_prova` yaz, `web`'i başlat.
   Eski veritabanı inceleme için yerinde kalır; iş bitince silinir. (İstenirse adlar sqlcmd ile
   `ALTER DATABASE … MODIFY NAME` ile değiştirilir; bağlantı dizesi o zaman değişmez.)
4. **Görselleri geri koy** (gerekiyorsa) — arşiv volume'a açılır, dosya sahipliği korunur:
   ```sh
   docker compose -f docker-compose.prod.yml stop web
   docker run --rm -v "$(docker volume ls -q | grep heryerde-uploads):/hedef" -v "$PWD/backups:/yedek:ro" alpine \
     sh -c 'tar -xzf /yedek/uploads-20260915-000000.tar.gz -C /hedef'
   # Fatura ve dekontlar aynı yolla heryerde-private volume'una: belgeler-20260915-000000.tar.gz
   docker compose -f docker-compose.prod.yml start web
   ```
5. `pwsh tools/smoke.ps1 -BaseUrl https://alanadi.com` ile vitrini denetle.

## Prova (otomatik)
CI'daki haftalık `restore-check` job'u (pazartesi 06:00 UTC ve Actions › CI › Run workflow) prod imajını kaldırır,
`--ithal` ile örnek ürün girer, yedek alır, son `.bak`'ı `HerYerde_prova`'ya açar; ürün sayısının eşit olduğunu ve
prova veritabanıyla açılan uygulamanın `/health/ready` için 200 döndüğünü doğrular. Bu, gerçek yedeklerin değil
yedek/geri yükleme **yolunun** provasıdır; canlı yedeğin kendisi yayın sonrası ayda bir elle provaya açılır
(docs/yayin-kontrol.md).

## Harici kopya
Aynı sunucudaki `backups/` klasörü disk ya da sunucu kaybına karşı korumaz. **[Ömer]** karar verip buraya yazar:
- Hedef (ör. sağlayıcının nesne deposu / ayrı bölgedeki depolama): _karar bekliyor_
- Aktarım ve sıklık (ör. gecelik yedekten sonra `rclone copy backups/ uzak:heryerde-yedek`): _karar bekliyor_
- Şifreleme ve erişim (yedek kişisel veri içerir; KVKK saklama süreleri docs/veri-envanteri.md): _karar bekliyor_
- Uzak kopyada saklama süresi: _karar bekliyor_
