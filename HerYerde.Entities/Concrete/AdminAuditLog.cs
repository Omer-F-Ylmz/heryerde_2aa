using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Yönetici denetim izi: kim, hangi kayıtta ne yaptı, ne zaman, hangi IP'den. Yönetici silinse de
/// iz kalsın diye admin_user'a yabancı anahtar yok.</summary>
public class AdminAuditLog : IEntity
{
    public int Id { get; set; }
    public int AdminId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public DateTime At { get; set; }
    public string? Ip { get; set; }
}
