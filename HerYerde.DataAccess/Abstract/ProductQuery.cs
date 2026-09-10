namespace HerYerde.DataAccess.Abstract;

public enum ProductOrder
{
    /// <summary>En yeni önce.</summary>
    Newest,

    /// <summary>Kampanyalıda kampanya fiyatı esas alınarak artan.</summary>
    Price
}

/// <summary>Vitrin listesi: süzme, sıralama ve sayfalama tek SQL sorgusuna çevrilir.</summary>
public sealed record ProductQuery
{
    /// <summary>Boşsa kategori süzgeci uygulanmaz.</summary>
    public IReadOnlyCollection<int> CategoryIds { get; init; } = [];

    public int? ExcludedProductId { get; init; }

    /// <summary>Doluysa ad, açıklama ya da kategori adında geçen ürünler; LIKE ile SQL'de.</summary>
    public string? Term { get; init; }

    /// <summary>Etkin fiyat (kampanya sürüyorsa kampanya fiyatı) bu aralıkta kalır.</summary>
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }

    public ProductOrder Order { get; init; } = ProductOrder.Newest;

    /// <summary>Kampanyanın süresi dolmuş mu kararı bu ana göre verilir.</summary>
    public DateTime Now { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = int.MaxValue;
}
