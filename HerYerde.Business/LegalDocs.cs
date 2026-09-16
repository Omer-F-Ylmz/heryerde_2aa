namespace HerYerde.Business;

/// <summary>Yasal metinlerin (sözleşme, ön bilgilendirme, aydınlatma, politikalar) yürürlükteki sürümü.
/// Metin değişince sürüm ve tarih birlikte ilerler; sipariş onayladığı sürümü saklar.</summary>
public static class LegalDocs
{
    public const string Version = "2026-09-16.2";
    public static readonly DateTime UpdatedAt = new(2026, 9, 16);

    /// <summary>Sürümün ön bilgilendirme + mesafeli satış sözleşmesi PDF'inin gizli depodaki yolu; sürüm yürürlükteyken üretilip
    /// arşivlenir, metin sonradan değişse de o sürümü onaylayan siparişin belgesi aynı kalır.</summary>
    public static string ArchivePath(string version) => $"sozlesmeler/{version}.pdf";
}
