using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Ürün videosu. Yüklenen dosya sunucuda yeniden kodlanır: adresler her zaman depo biçimindedir.</summary>
public class ProductVideo : IEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }

    /// <summary>720p H.264 mp4.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>İlk kareden üretilen kapak görseli; video oynatılmadan önce bu gösterilir.</summary>
    public string PosterUrl { get; set; } = string.Empty;

    /// <summary>Kartta üzerine gelince oynayan sessiz 3 saniyelik webm.</summary>
    public string PreviewUrl { get; set; } = string.Empty;

    /// <summary>Kaynağın saniye cinsinden süresi; VideoObject için gerekir.</summary>
    public int Duration { get; set; }
    public int SortOrder { get; set; }

    /// <summary>VideoObject işaretlemesi yükleme tarihini ister.</summary>
    public DateTime CreatedAt { get; set; }
}
