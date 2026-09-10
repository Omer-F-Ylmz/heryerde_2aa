# HerYerde — karma e-ticaret (.NET 10 MVC)

Karma mağaza, iki kök alan: **Giyim** (şalvar; beden/renk varyantlı) ve **Ev** (mutfak-sofra,
saklama, sepet, küçük ev aleti, dekor; çoğu varyantsız). İkisi de aynı `Product`/`ProductVariant`
modelinde — varyantsız üründe `size`/`color` NULL kalır.

## Mimari (Cafixo katmanları)
`HerYerde.Web` (MVC, Autofac, `/health`) → `HerYerde.Business` (`I{X}Service` / `{X}Manager`) →
`HerYerde.DataAccess` (`I{X}Dal` / `Ef{X}Dal : EfEntityRepositoryBase`, `EfUnitOfWork`, migrations) →
`HerYerde.Entities` (flat, nav prop yok, snake_case kolon) → `HerYerde.Core`.
- DI: `AutofacBusinessModule` içinde manuel `RegisterType`; assembly scan yok.
- Okuma `GetAsync` (izlemez); değiştirilip kaydedilecek kayıt `GetTrackedAsync`.
- DB: EF Core + SQL Server; dev LocalDB. `appsettings.Development.json` (gitignore) ← `.example.json`.
- Kültür tr-TR; ondalık bağlama `InvariantDecimalModelBinder` ile noktalı kalır (1290.50).
- CSS: Tailwind CLI `src/input.css` → `HerYerde.Web/wwwroot/css/site.css` (`npm run css:build`);
  `_Layout.cshtml` tek kaynak, inline `<style>` yasak.
- Testler: `tests/HerYerde.Tests` (xUnit), gerçek SQL Server'a karşı.

## Süreç
1. Kırmızı-önce: önce başarısız test, sonra kod. Kanıtsız "bitti" yok.
2. `dotnet build HerYerde.sln -warnaserror` → 0 uyarı 0 hata.
3. `dotnet test HerYerde.sln` → yeşil; simülasyon/uydurma çıktı yok.
4. Migration: `dotnet ef migrations add <Ad> -p HerYerde.DataAccess -s HerYerde.Web`.
5. CI (`.github/workflows/ci.yml`) yeşil olmadan iş kapanmaz.
6. Her dalga `/clear` ile yeni oturumda başlar.
7. UI'a (Razor/CSS/Tailwind) dokunan her işte `/frontend-craft` zorunlu; Bölüm 6 tur raporu
   olmadan kapanış yok. Marka kararı: `brand.md`.
8. En basit çözüm, cerrahi değişiklik: istenmeyen özellik/soyutlama/konfigürasyon eklenmez.
9. Kapanış raporu tek biçim: **commit · test sayısı · CI · sapmalar** (sapma yoksa "yok" yazılır).
   Rapor yalnız bu dört başlıktır; tablo, KANIT bölümü, tur dökümü, ölçüm listesi YAZILMAZ.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
