using System.Text.Json;
using System.Text.Json.Serialization;

namespace HerYerde.Web.Models;

/// <summary>81 il ve ilçeleri (TÜİK ADNKS; kaynak dosyanın "kaynak" alanında, koşullar docs/lisans-notlari.md);
/// ödeme formundaki bağımlı seçim buradan beslenir. Kayıtlı siparişin il/ilçesi
/// serbest metindir: bu liste yalnız yeni siparişin formunu doldurur, eski kayıtları doğrulamaz.</summary>
public interface IProvinceDirectory
{
    IReadOnlyList<string> Names { get; }

    /// <summary>İl listede yoksa boş liste.</summary>
    IReadOnlyList<string> DistrictsOf(string? province);
}

public sealed class ProvinceDirectory : IProvinceDirectory
{
    /// <summary>wwwroot altındaki göreli yol.</summary>
    public const string FileName = "data/il-ilce.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, IReadOnlyList<string>> _districts;

    public ProvinceDirectory(string webRootPath)
    {
        var path = Path.Combine(webRootPath, FileName.Replace('/', Path.DirectorySeparatorChar));
        var file = File.Exists(path)
            ? JsonSerializer.Deserialize<ProvinceFile>(File.ReadAllBytes(path), Json)
            : null;
        var provinces = file?.Iller ?? [];

        Names = provinces.Select(p => p.Ad).ToList();
        // Ordinal: il adı listedeki seçenekten geldiği için birebir eşleşir; Türkçe İ/ı kıyası gerekmiyor.
        _districts = provinces.ToDictionary(p => p.Ad, p => (IReadOnlyList<string>)p.Ilceler, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> Names { get; }

    public IReadOnlyList<string> DistrictsOf(string? province)
        => province is { Length: > 0 } && _districts.TryGetValue(province.Trim(), out var districts) ? districts : [];

    private sealed record ProvinceFile([property: JsonPropertyName("iller")] IReadOnlyList<ProvinceRow> Iller)
    {
        public ProvinceFile() : this([])
        {
        }
    }

    private sealed record ProvinceRow(
        [property: JsonPropertyName("kod")] int Kod,
        [property: JsonPropertyName("ad")] string Ad,
        [property: JsonPropertyName("ilceler")] IReadOnlyList<string> Ilceler);
}
