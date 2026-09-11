namespace HerYerde.Business.Rules;

/// <summary>Anonimleştirmede kişisel verinin yerine yazılan maske: kişi okunmaz, alanın türü anlaşılır.</summary>
public static class PersonalDataMask
{
    public const string Hidden = "***";

    /// <summary>"Ayşe Yılmaz" → "A*** Y***".</summary>
    public static string Name(string fullName)
        => string.Join(' ', fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => word[0] + Hidden));

    /// <summary>"05424970982" → "05*******82"; uzunluk korunur.</summary>
    public static string Phone(string phone)
        => phone.Length <= 4 ? Hidden : phone[..2] + new string('*', phone.Length - 4) + phone[^2..];

    /// <summary>"ayse@example.com" → "a***@***".</summary>
    public static string Email(string email) => email[0] + Hidden + "@" + Hidden;
}
