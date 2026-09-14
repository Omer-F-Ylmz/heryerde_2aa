using HerYerde.Core.Entities;
using HerYerde.Entities.Enums;

namespace HerYerde.Entities.Concrete;

/// <summary>İletişim formundan gelen mesaj. Kayıtla aynı işlemde mağazaya e-posta kuyruğa yazılır.</summary>
public class ContactMessage : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Müşterinin yazdığı e-posta ya da telefon; biçimi zorlanmaz.</summary>
    public string Contact { get; set; } = string.Empty;

    public ContactSubject Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>Yönetici "okundu" işaretleyince dolar.</summary>
    public DateTime? ReadAt { get; set; }
}
