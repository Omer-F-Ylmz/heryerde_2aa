using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Dtos;

public sealed record GiftRegistryDraft(
    string OwnerName,
    string Phone,
    string? Email,
    DateTime EventDate,
    string? Message,
    bool IsPublic = true);

/// <summary>Listedeki bir ürün: vitrin bilgisi anlık okunur, adetler kalemden gelir.
/// Available: ürün yayında ve (varyantlıysa) varyant duruyor; değilse hediye edilemez.</summary>
public sealed record GiftRegistryLine(
    int ItemId,
    int ProductId,
    string ProductName,
    string Slug,
    string? ImageUrl,
    string? Size,
    string? Color,
    int DesiredQty,
    int ReceivedQty,
    bool Available)
{
    public int Remaining => Math.Max(0, DesiredQty - ReceivedQty);

    public bool Completed => Remaining == 0;
}

public sealed record GiftRegistryView(GiftRegistry Registry, IReadOnlyList<GiftRegistryLine> Lines)
{
    public int Desired => Lines.Sum(l => l.DesiredQty);

    /// <summary>İstenenden fazla alınan adet ilerlemeyi %100'ün üstüne taşımaz.</summary>
    public int Received => Lines.Sum(l => Math.Min(l.ReceivedQty, l.DesiredQty));
}

/// <summary>Yönetim listesi satırı.</summary>
public sealed record GiftRegistrySummary(GiftRegistry Registry, int ItemCount, int Desired, int Received);
