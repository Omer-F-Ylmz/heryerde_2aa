using HerYerde.Core.Entities;
using HerYerde.Entities.Enums;

namespace HerYerde.Entities.Concrete;

public class Order : IEntity
{
    public int Id { get; set; }

    /// <summary>"HY-yyyyMMdd-####"; gün içinde sıra numarası, tabloda tekil.</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>Teşekkür sayfasının anahtarı; sipariş numarası tahmin edilebilir olduğu için erişim buna bakar.</summary>
    public Guid AccessToken { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Ödeme adımında ön bilgilendirme formu ve mesafeli satış sözleşmesinin onaylandığı an.</summary>
    public DateTime? ConsentAt { get; set; }

    /// <summary>Onaylanan yasal metinlerin sürümü; bu alan eklenmeden önceki siparişte boş.</summary>
    public string? LegalVersion { get; set; }
}
