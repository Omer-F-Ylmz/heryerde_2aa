using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

public class GiftRegistryItem : IEntity
{
    public int Id { get; set; }
    public int GiftRegistryId { get; set; }
    public int ProductId { get; set; }

    /// <summary>Varyantlı üründe sahibin seçtiği beden/renk; varyantsızda boş.</summary>
    public int? VariantId { get; set; }
    public int DesiredQty { get; set; }

    /// <summary>Sipariş kesinleşince artar (havalede sipariş anında, kartta ödeme onayında).</summary>
    public int ReceivedQty { get; set; }
}
