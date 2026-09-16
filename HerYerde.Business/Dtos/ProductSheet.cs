namespace HerYerde.Business.Dtos;

/// <summary>Toplu ürün tablosunun bir satırı, hücreler metin olarak (sayılar nokta ondalıklı). Sku boşsa ürün satırı,
/// doluysa aynı slug'lı ürünün varyant satırıdır; varyant satırında ürün sütunları yok sayılır. ExportedStock: dosya
/// dışa aktarmadan geldiyse o andaki stok; hücre buna eşitse stok yazılmaz (arada satılan adet ezilmesin).</summary>
public sealed record ProductSheetRow(
    int RowNumber,
    string Id,
    string Name,
    string Slug,
    string CategorySlug,
    string Description,
    string Price,
    string CampaignPrice,
    string CampaignLabel,
    string CampaignEndsAt,
    string Stock,
    string IsActive,
    string Sku,
    string Axis1,
    string Axis2,
    string VariantStock,
    string Images,
    string Dimensions,
    string Attributes = "",
    string? ExportedStock = null)
{
    public bool IsVariant => Sku.Length > 0;
}

public enum ImportRowKind
{
    Yeni = 1,
    Guncelle = 2,
    Hata = 3
}

/// <summary>Label: satırı tanıtan slug ya da stok kodu; Reason yalnız hatalı satırda dolu.</summary>
public sealed record ImportRowResult(int RowNumber, ImportRowKind Kind, string Label, string? Reason);

/// <summary>RemovedImages: uygulamada listeden düşen görsellerin adresleri; dosyalarını silmek çağıranın işi
/// (depo Web katmanında, iş katmanı diske dokunmaz).</summary>
public sealed record ImportPreview(IReadOnlyList<ImportRowResult> Rows, IReadOnlyList<string>? RemovedImages = null)
{
    public IReadOnlyList<string> Removed => RemovedImages ?? [];

    public int Created => Rows.Count(r => r.Kind == ImportRowKind.Yeni);
    public int Updated => Rows.Count(r => r.Kind == ImportRowKind.Guncelle);
    public int Errors => Rows.Count(r => r.Kind == ImportRowKind.Hata);
}
