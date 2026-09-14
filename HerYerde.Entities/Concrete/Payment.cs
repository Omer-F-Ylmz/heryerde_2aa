using HerYerde.Core.Entities;
using HerYerde.Entities.Enums;

namespace HerYerde.Entities.Concrete;

/// <summary>Kartla ödenen siparişin sağlayıcı kaydı. Kart bilgisi burada tutulmaz.</summary>
public class Payment : IEntity
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    /// <summary>Ödeme onaylanınca boşaltılacak sepet; dönüş isteği çapraz site POST olduğu için çerez taşımaz.</summary>
    public Guid? CartId { get; set; }

    public string Provider { get; set; } = string.Empty;

    /// <summary>Sağlayıcıya giden ve dönüşte eşleşen anahtar; tabloda tekil.</summary>
    public string ConversationId { get; set; } = string.Empty;

    public string? PaymentId { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Sağlayıcı yanıtı; kart numarası ve CVC maskelenmiş, kırpılmış.</summary>
    public string? RawResponse { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
