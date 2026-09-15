using System.ComponentModel.DataAnnotations;
using HerYerde.Entities.Enums;

namespace HerYerde.Web.Models;

public sealed class ContactFormViewModel
{
    [Required(ErrorMessage = "Adınızı yazın.")]
    [StringLength(120, ErrorMessage = "Ad en fazla 120 karakter olabilir.")]
    [Display(Name = "Ad soyad")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Size nasıl dönelim? E-posta ya da telefon yazın.")]
    [StringLength(200, ErrorMessage = "En fazla 200 karakter.")]
    [Display(Name = "E-posta ya da telefon")]
    public string Contact { get; set; } = string.Empty;

    [EnumDataType(typeof(ContactSubject), ErrorMessage = "Konu seçin.")]
    [Display(Name = "Konu")]
    public ContactSubject Subject { get; set; } = ContactSubject.Siparis;

    [Required(ErrorMessage = "Mesajınızı yazın.")]
    [StringLength(2000, ErrorMessage = "Mesaj en fazla 2000 karakter olabilir.")]
    [Display(Name = "Mesajınız")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Bot tuzağı: görünmez alan; dolu gelen gönderim kaydedilmeden başarılı gibi yanıtlanır.</summary>
    public string? Website { get; set; }
}

/// <summary>/siparis-sorgula formu. Telefon geri yazılmaz; hata mesajı tek biçimdir.</summary>
public sealed class OrderLookupViewModel
{
    [StringLength(30)]
    public string? OrderNo { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    /// <summary>Bot tuzağı: dolu gelirse eşleşme aranmaz, yanlış bilgiyle aynı yanıt döner.</summary>
    public string? Website { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed record AboutPageVm(string WhatsAppUrl);

/// <summary>Sent: gönderim alındı, form yerine teşekkür gösterilir.</summary>
public sealed record ContactPageVm(ContactFormViewModel Form, bool Sent, string WhatsAppUrl);
