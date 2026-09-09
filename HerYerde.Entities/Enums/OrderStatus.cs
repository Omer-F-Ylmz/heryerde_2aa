namespace HerYerde.Entities.Enums;

/// <summary>Sipariş yaşam döngüsü. Geçişler sıralı ilerler; iptal yalnız <see cref="Beklemede"/> anında yapılır.</summary>
public enum OrderStatus
{
    Beklemede = 1,
    Onaylandi = 2,
    Kargoda = 3,
    TeslimEdildi = 4,
    IptalEdildi = 5
}
