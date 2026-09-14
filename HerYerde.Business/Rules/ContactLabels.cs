using HerYerde.Entities.Enums;

namespace HerYerde.Business.Rules;

/// <summary>İletişim konusunun görünen adı; form, e-posta ve yönetim listesi aynı dili konuşsun.</summary>
public static class ContactLabels
{
    public static string Subject(ContactSubject subject) => subject switch
    {
        ContactSubject.Siparis => "Sipariş",
        ContactSubject.Urun => "Ürün",
        ContactSubject.Iade => "İade",
        _ => "Diğer"
    };
}
