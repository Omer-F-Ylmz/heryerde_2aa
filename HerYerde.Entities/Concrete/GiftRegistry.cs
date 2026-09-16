using HerYerde.Core.Entities;

namespace HerYerde.Entities.Concrete;

/// <summary>Çeyiz listesi: sahibi ürün seçer, bağlantıyı alanlar listeden hediye alır. Üyelik yok; sahip tokenli
/// yönetim bağlantısıyla, ziyaretçi tahmin edilemeyen kısa adresle erişir.</summary>
public class GiftRegistry : IEntity
{
    public int Id { get; set; }

    /// <summary>Paylaşılan adresin 10 karakterlik rastgele parçası (/ceyizlistesi/{slug}).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Yönetim bağlantısının anahtarı; yalnız sahibine (posta/WhatsApp) gider.</summary>
    public Guid ManageToken { get; set; }
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Normalleştirilmiş cep telefonu; sahibin kendi listesinden almasını engellemek için de kullanılır.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Boşsa "hediye alındı" postası gitmez.</summary>
    public string? Email { get; set; }
    public DateTime EventDate { get; set; }
    public string? Message { get; set; }

    /// <summary>Kapalıysa paylaşılan adres 404 verir; sahip yönetim sayfasından yeniden açar.</summary>
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Girişliyken açıldıysa ya da e-postası doğrulanan üyeninkiyle aynıysa üye; "Çeyiz listelerim"de listelenir.</summary>
    public int? CustomerId { get; set; }
}
