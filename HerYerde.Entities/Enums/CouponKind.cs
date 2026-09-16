namespace HerYerde.Entities.Enums;

public enum CouponKind
{
    /// <summary>Ara toplamın yüzdesi; Value = yüzde (10 = %10).</summary>
    Yuzde = 1,

    /// <summary>Sabit tutar; Value = TL. Ara toplamı aşamaz.</summary>
    Tutar = 2,

    /// <summary>Kargo bedava; Value kullanılmaz, sipariş kargosu 0 olur.</summary>
    KargoBedava = 3
}
