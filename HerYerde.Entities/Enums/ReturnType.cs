namespace HerYerde.Entities.Enums;

/// <summary>Talep türü: cayma/iade (para geri) ya da aynı ürünün başka varyantıyla değişim.</summary>
public enum ReturnType
{
    Iade = 1,
    Degisim = 2
}

/// <summary>Bekliyor → Onaylandı → Teslim alındı → Tamamlandı; ret yalnız Bekliyor'dan. Kartlı iadede ve değişimde
/// teslim alınınca doğrudan Tamamlandı olur; havale/kapıda ödemede yöneticinin "iade edildi" işaretini bekler.</summary>
public enum ReturnStatus
{
    Bekliyor = 1,
    Onaylandi = 2,
    Reddedildi = 3,
    TeslimAlindi = 4,
    Tamamlandi = 5
}
