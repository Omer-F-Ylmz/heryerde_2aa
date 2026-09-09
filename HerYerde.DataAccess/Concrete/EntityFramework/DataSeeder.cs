using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.DataAccess.Concrete.EntityFramework;

/// <summary>Açılış kataloğu: Giyim/Ev kökleri, 6 Ev alt kategorisi, 18 ürün. Ev altında kategori varsa dokunmaz.</summary>
public static class DataSeeder
{
    public const string HeroCampaignName = "Granit döküm tencere seti";
    public const string ExpiredCampaignName = "Aprilla ayaklı vantilatör";

    public static async Task SeedCatalogAsync(HerYerdeContext context, CancellationToken cancellationToken = default)
    {
        var ev = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "ev", cancellationToken);
        if (ev is not null && await context.Categories.AnyAsync(c => c.ParentId == ev.Id, cancellationToken))
        {
            return;
        }

        if (await context.Categories.FirstOrDefaultAsync(c => c.Slug == "giyim", cancellationToken) is null)
        {
            context.Categories.Add(new Category { Name = "Giyim", Slug = "giyim", SortOrder = 1, IsActive = true });
        }

        if (ev is null)
        {
            ev = new Category { Name = "Ev", Slug = "ev", SortOrder = 2, IsActive = true };
            context.Categories.Add(ev);
        }

        await context.SaveChangesAsync(cancellationToken);

        var subs = new (string Name, string Slug)[]
        {
            ("Tencere & Tava", "tencere-tava"),
            ("Yemek Takımı", "yemek-takimi"),
            ("Çatal-Kaşık", "catal-kasik"),
            ("Saklama & Düzenleme", "saklama-duzenleme"),
            ("Sepet & Dekor", "sepet-dekor"),
            ("Küçük Ev Aletleri", "kucuk-ev-aletleri")
        };
        var categories = subs.Select((s, i) => new Category { Name = s.Name, Slug = s.Slug, ParentId = ev.Id, SortOrder = i + 1, IsActive = true }).ToList();
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync(cancellationToken);
        var bySlug = categories.ToDictionary(c => c.Slug, c => c.Id);

        var now = DateTime.UtcNow;
        var products = new List<Product>();
        var order = 0;

        void Add(string sub, string name, string slug, decimal price, string description, decimal? campaign = null, string? label = null, DateTime? endsAt = null)
        {
            products.Add(new Product
            {
                Name = name,
                Slug = slug,
                Description = description,
                CategoryId = bySlug[sub],
                Price = price,
                CampaignPrice = campaign,
                CampaignLabel = label,
                CampaignEndsAt = endsAt,
                IsActive = true,
                CreatedAt = now.AddMinutes(-order++),
                UpdatedAt = now
            });
        }

        Add("tencere-tava", "Taç Sera Feel 3'lü sahan seti", "tac-sera-feel-3lu-sahan-seti", 1890m, "16, 20 ve 24 cm sahan; cam kapaklı, granit kaplama, indüksiyon uyumlu.");
        Add("tencere-tava", HeroCampaignName, "granit-dokum-tencere-seti", 3190m, "7 parça: 20/24/28 cm tencere, 24 cm derin tava; kalın taban, yapışmaz iç yüzey.", 2490m, "Çeyiz kampanyası", now.AddDays(2));
        Add("tencere-tava", "Çelik düdüklü tencere 7 L", "celik-duduklu-tencere-7-l", 2150m, "Paslanmaz çelik, çift emniyet valfi, tüm ocaklarda kullanılır.");
        Add("tencere-tava", "Döküm tava 28 cm", "dokum-tava-28-cm", 1290m, "Ağır döküm, ısıyı eşit dağıtır; fırında da kullanılır.");
        Add("yemek-takimi", "24 parça porselen yemek takımı", "24-parca-porselen-yemek-takimi", 4290m, "6 kişilik: servis tabağı, yemek tabağı, çorba kâsesi, tatlı tabağı; bulaşık makinesine uygun.");
        Add("yemek-takimi", "12 parça kahvaltı takımı", "12-parca-kahvalti-takimi", 1690m, "6 kişilik: kahvaltı tabağı ve kâse; krem sır.");
        Add("yemek-takimi", "6'lı çay bardağı seti", "6li-cay-bardagi-seti", 390m, "İnce belli klasik bardak, tabaklı; ısıya dayanıklı cam.", 320m, "Haftanın fırsatı");
        Add("catal-kasik", "60 parça Felina çatal-kaşık takımı", "60-parca-felina-catal-kasik-takimi", 2790m, "12 kişilik; yemek, tatlı ve çay kaşığı, çatal, bıçak; 18/10 çelik, kutulu.");
        Add("catal-kasik", "6'lı servis kaşığı seti", "6li-servis-kasigi-seti", 540m, "Kepçe, spatula, servis kaşığı ve çatal; çelik, askılı.");
        Add("catal-kasik", "12 parça tatlı çatalı", "12-parca-tatli-catali", 320m, "Altın rengi kaplama, çeyiz kutulu.");
        Add("saklama-duzenleme", "Bambu kapaklı baharatlık", "bambu-kapakli-baharatlik", 420m, "12'li cam baharatlık, döner standlı, etiket seti dahil.");
        Add("saklama-duzenleme", "Cam saklama kabı 5'li set", "cam-saklama-kabi-5li-set", 690m, "Borosilikat cam, kilitli kapak, fırın ve dondurucuya uygun.");
        Add("saklama-duzenleme", "Erzak kavanozu 4'lü", "erzak-kavanozu-4lu", 480m, "1,5 L cam kavanoz, ahşap kapak, hava geçirmez conta.");
        Add("sepet-dekor", "Hasır piknik sepeti", "hasir-piknik-sepeti", 890m, "El örgüsü hasır, çift kapaklı, kumaş astarlı.");
        Add("sepet-dekor", "Hasır çamaşır sepeti", "hasir-camasir-sepeti", 1190m, "Kapaklı, 60 cm, iç astarı çıkarılabilir.");
        Add("sepet-dekor", "Örgü ekmek sepeti 3'lü", "orgu-ekmek-sepeti-3lu", 350m, "Üç boy, doğal hasır, pamuklu bez ile.");
        Add("kucuk-ev-aletleri", ExpiredCampaignName, "aprilla-ayakli-vantilator", 1990m, "40 cm, 3 kademe, salınımlı, uzaktan kumandalı.", 1590m, "Yaz sonu", now.AddDays(-1));
        Add("kucuk-ev-aletleri", "El blenderı seti", "el-blenderi-seti", 1450m, "1000 W, çelik ayak, doğrayıcı ve çırpıcı başlıklı.");

        context.Products.AddRange(products);
        await context.SaveChangesAsync(cancellationToken);
    }
}
