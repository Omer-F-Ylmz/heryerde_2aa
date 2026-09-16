using System.Globalization;

namespace HerYerde.Web.Models;

public static class Money
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    /// <summary>1290.5 → "1.290,50 ₺".</summary>
    public static string Tl(decimal amount) => amount.ToString("N2", Turkish) + " ₺";
}

public enum BadgeKind
{
    Campaign,
    Gift,
    Low,
    SoldOut
}

public sealed record BadgeVm(BadgeKind Kind, string Text);

public sealed record PriceBlockVm(decimal Price, decimal? CampaignPrice, bool Large = false)
{
    public bool HasCampaign => CampaignPrice is { } campaign && campaign < Price;
}

/// <summary>FRONT kiti: taksit tablosu. BankName doluysa tablo girilen kartın bankasına aittir.</summary>
public sealed record InstallmentTableVm(
    IReadOnlyList<HerYerde.Business.Dtos.InstallmentOption> Options,
    string? BankName = null,
    string? Note = null);

public sealed class ProductCardVm
{
    /// <summary>Kalp düğmesi için; 0 ise (stil rehberi örnekleri) kalp çizilmez.</summary>
    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? CampaignPrice { get; init; }
    public string? ImageUrl { get; init; }
    public string? SecondImageUrl { get; init; }

    /// <summary>Kartın üzerine gelince oynayan sessiz webm; ürünün videosu yoksa null.</summary>
    public string? PreviewUrl { get; init; }
    public string ImageAlt { get; init; } = string.Empty;
    public BadgeVm? Badge { get; init; }
    public string? PlaceholderIcon { get; init; }
    public bool Lazy { get; init; } = true;
    public string Url => "/urun/" + Slug;
}

/// <summary>Via: alıntının kaynağı; sabit DM'lerde "Instagram DM", onaylı yorumda ürün adı.</summary>
public sealed record TestimonialVm(string Name, string Quote, string Via = "Instagram DM");

public sealed record VariantChipVm(string Label, int Stock);

/// <summary>Sepete eklemede beden+renk seçimini varyant kimliğine çevirmek için.</summary>
public sealed record VariantOptionVm(int Id, string? Size, string? Color, int Stock);

/// <summary>Axis1/Axis2: eksenlerin vitrin adı (ürünün VariantAxis1Label/2Label); boşsa Beden/Renk.</summary>
public sealed record VariantPickerVm(
    IReadOnlyList<VariantChipVm> Sizes,
    IReadOnlyList<VariantChipVm> Colors,
    IReadOnlyList<VariantOptionVm>? Options = null,
    string? Axis1Label = null,
    string? Axis2Label = null)
{
    public bool HasOptions => Options is { Count: > 0 };
}

/// <summary>ImageUrl: sekmede küçük kare görsel (kategorinin 1:1 kesiti); yoksa yalnız metin.</summary>
public sealed record CategoryTabVm(string Name, string Url, bool Current, string? ImageUrl = null);

/// <summary>Süzgeç formunda korunacak alan (arama terimi, sıralama).</summary>
public sealed record HiddenFieldVm(string Name, string Value);

/// <summary>FRONT kiti: yeni/fiyat sıralama sekmeleri; bağlantılar süzgeçleri korur.</summary>
public sealed record SortTabsVm(string NewUrl, string PriceUrl, bool ByPrice);

public sealed record PaginationVm(int Page, int TotalPages, string BaseUrl)
{
    public string UrlFor(int page)
        => page <= 1 ? BaseUrl : $"{BaseUrl}{(BaseUrl.Contains('?') ? '&' : '?')}sayfa={page}";
}
