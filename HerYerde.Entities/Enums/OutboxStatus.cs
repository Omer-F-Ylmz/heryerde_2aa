namespace HerYerde.Entities.Enums;

/// <summary>Kuyruktaki bildirimin durumu; <see cref="Basarisiz"/> üç denemeden sonra kalıcıdır.</summary>
public enum OutboxStatus
{
    Bekliyor = 1,
    Gonderildi = 2,
    Basarisiz = 3
}
