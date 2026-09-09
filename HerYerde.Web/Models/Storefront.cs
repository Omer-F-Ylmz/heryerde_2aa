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
    Low
}

public sealed record BadgeVm(BadgeKind Kind, string Text);

public sealed record PriceBlockVm(decimal Price, decimal? CampaignPrice, bool Large = false)
{
    public bool HasCampaign => CampaignPrice is { } campaign && campaign < Price;
}

public sealed class ProductCardVm
{
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? CampaignPrice { get; init; }
    public string? ImageUrl { get; init; }
    public string? SecondImageUrl { get; init; }
    public string ImageAlt { get; init; } = string.Empty;
    public BadgeVm? Badge { get; init; }
    public string? PlaceholderIcon { get; init; }
    public bool Lazy { get; init; } = true;
    public string Url => "/urun/" + Slug;
}

public sealed record TestimonialVm(string Name, string Quote);

public sealed record VariantChipVm(string Label, int Stock);

/// <summary>Sepete eklemede beden+renk seçimini varyant kimliğine çevirmek için.</summary>
public sealed record VariantOptionVm(int Id, string? Size, string? Color, int Stock);

public sealed record VariantPickerVm(
    IReadOnlyList<VariantChipVm> Sizes,
    IReadOnlyList<VariantChipVm> Colors,
    IReadOnlyList<VariantOptionVm>? Options = null);

public sealed record CategoryTabVm(string Name, string Url, bool Current);

public sealed record PaginationVm(int Page, int TotalPages, string BaseUrl)
{
    public string UrlFor(int page)
        => page <= 1 ? BaseUrl : $"{BaseUrl}{(BaseUrl.Contains('?') ? '&' : '?')}sayfa={page}";
}
