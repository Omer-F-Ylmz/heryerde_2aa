namespace HerYerde.Web.Models;

/// <summary>Yasal metin sayfası; görünüm adı slug'la aynıdır (Views/Legal/{slug}.cshtml).</summary>
public sealed record LegalPage(string Slug, string Title, string Description);

/// <summary>/yasal/{slug}, altbilgi ve sitemap aynı listeden okur.</summary>
public static class LegalPages
{
    public static readonly IReadOnlyList<LegalPage> All =
    [
        new("kvkk-aydinlatma", "KVKK aydınlatma metni",
            "Hangi kişisel verileri hangi amaçla ve ne kadar süre işlediğimiz, haklarınız ve başvuru kanalları."),
        new("gizlilik-politikasi", "Gizlilik politikası",
            "Sipariş verirken paylaştığınız bilgileri nasıl kullandığımız, kimlerle paylaştığımız ve nasıl koruduğumuz."),
        new("cerez-politikasi", "Çerez politikası",
            "Yalnız zorunlu çerezler: sepet, form güvenliği, son sipariş ve yönetici oturumu. Üçüncü taraf çerez yok."),
        new("mesafeli-satis-sozlesmesi", "Mesafeli satış sözleşmesi",
            "Ürün, fiyat, ödeme, teslimat, 14 gün cayma hakkı ve iade koşulları."),
        new("on-bilgilendirme-formu", "Ön bilgilendirme formu",
            "Sipariş vermeden önce: satıcı bilgileri, fiyat, kargo, ödeme, teslimat ve cayma hakkı."),
        new("teslimat-ve-iade", "Teslimat ve iade",
            "Kargo ücreti, kapıda ödeme ve havale/EFT, teslim süresi, 14 gün cayma hakkı ve WhatsApp'tan iade.")
    ];

    public static LegalPage? Find(string slug) => All.FirstOrDefault(page => page.Slug == slug);
}
