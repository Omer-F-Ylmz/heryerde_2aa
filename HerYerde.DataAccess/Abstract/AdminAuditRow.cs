using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

/// <summary>Denetim kaydı ve yöneticinin e-postası; yönetici silinmişse e-posta boş kalır.</summary>
public sealed record AdminAuditRow(AdminAuditLog Log, string? AdminEmail);
