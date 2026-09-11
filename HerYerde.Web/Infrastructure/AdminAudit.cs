using System.Security.Claims;
using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Web.Infrastructure;

public static class AdminAudit
{
    /// <summary>Başarılı yönetim işleminin izini yazar: yönetici çerezden, IP bağlantıdan (vekil arkasında
    /// ForwardedHeaders ile gerçek istemci). İşlem yapılmış olduğundan istemci kopsa da iz yazılır.</summary>
    public static Task WriteAsync(this IAdminAuditService audit, HttpContext context, string action, string entity, int? entityId)
        => audit.LogAsync(new AdminAuditLog
        {
            AdminId = int.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Ip = context.Connection.RemoteIpAddress?.ToString()
        }, CancellationToken.None);
}
