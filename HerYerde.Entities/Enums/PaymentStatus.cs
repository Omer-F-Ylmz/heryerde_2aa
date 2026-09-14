namespace HerYerde.Entities.Enums;

/// <summary>Kart ödemesinin durumu; yalnız <see cref="Baslatildi"/> kayıt kapanabilir (bir kez).</summary>
public enum PaymentStatus
{
    Baslatildi = 1,
    Basarili = 2,
    Basarisiz = 3,
    Iade = 4
}
