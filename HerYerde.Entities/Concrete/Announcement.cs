using HerYerde.Core.Entities;
using HerYerde.Entities.Enums;

namespace HerYerde.Entities.Concrete;

/// <summary>Site üstündeki duyuru şeridi. Aynı anda birden çok duyuru tanımlı olabilir; vitrinde tarih
/// aralığındaki en yeni etkin kayıt gösterilir.</summary>
public class Announcement : IEntity
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>Şeridin bağlantısı; yalnız site içi yol ("/ev"). Boşsa şerit bağlantısızdır.</summary>
    public string? Url { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public AnnouncementColor Color { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
