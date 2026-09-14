namespace HerYerde.Business.Rules;

/// <summary>Kargo bedeli tek yerden: sepet ile sipariş aynı eşiği kullansın diye.</summary>
public static class ShippingRules
{
    public static bool IsFree(decimal subtotal, decimal freeOver) => freeOver > 0m && subtotal >= freeOver;

    public static decimal Fee(decimal subtotal, decimal fee, decimal freeOver) => IsFree(subtotal, freeOver) ? 0m : fee;

    /// <summary>Bedava kargoya kalan tutar; eşik kapalıysa ya da aşıldıysa 0.</summary>
    public static decimal Remaining(decimal subtotal, decimal freeOver)
        => freeOver <= 0m ? 0m : Math.Max(0m, freeOver - subtotal);
}
