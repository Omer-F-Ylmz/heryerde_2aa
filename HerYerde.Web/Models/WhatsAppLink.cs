namespace HerYerde.Web.Models;

/// <summary>wa.me bağlantısı: numara uluslararası biçime çevrilir, mesaj adres kodlamasından geçer.</summary>
public static class WhatsAppLink
{
    public static string For(string phone, string message)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var international = digits.StartsWith('0') ? "90" + digits[1..] : digits;
        return $"https://wa.me/{international}?text={Uri.EscapeDataString(message)}";
    }
}
