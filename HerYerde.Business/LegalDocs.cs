namespace HerYerde.Business;

/// <summary>Yasal metinlerin (sözleşme, ön bilgilendirme, aydınlatma, politikalar) yürürlükteki sürümü.
/// Metin değişince sürüm ve tarih birlikte ilerler; sipariş onayladığı sürümü saklar.</summary>
public static class LegalDocs
{
    public const string Version = "2026-09-11";
    public static readonly DateTime UpdatedAt = new(2026, 9, 11);
}
