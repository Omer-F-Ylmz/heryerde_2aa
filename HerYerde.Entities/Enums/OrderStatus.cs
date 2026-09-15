namespace HerYerde.Entities.Enums;

/// <summary>Sipariş yaşam döngüsü: Beklemede → Onaylandı → Hazırlanıyor → Kargoda → Teslim edildi. Değerler veritabanında
/// saklandığı için sıra değil kimliktir (Hazırlanıyor sonradan eklendi); sıra OrderRules'tadır. İptal yalnız
/// <see cref="Beklemede"/> anında yapılır.</summary>
public enum OrderStatus
{
    Beklemede = 1,
    Onaylandi = 2,
    Kargoda = 3,
    TeslimEdildi = 4,
    IptalEdildi = 5,
    Hazirlaniyor = 6
}
