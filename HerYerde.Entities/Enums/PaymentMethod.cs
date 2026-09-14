namespace HerYerde.Entities.Enums;

/// <summary>Ödeme yöntemleri. Sipariş akışı D4'te; D1'de yalnız tanım (bkz. docs/odeme-teslimat.md).</summary>
public enum PaymentMethod
{
    /// <summary>Kapıda nakit/kart. Açılışta aktif.</summary>
    KapidaOdeme = 1,

    /// <summary>Havale/EFT ile ön ödeme. Açılışta aktif.</summary>
    HavaleEft = 2,

    /// <summary>Kredi kartı: İyzico 3D Secure; anahtar tanımlıysa açık.</summary>
    KrediKarti = 3
}
