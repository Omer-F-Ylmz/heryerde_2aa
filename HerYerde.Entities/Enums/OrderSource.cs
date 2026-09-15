namespace HerYerde.Entities.Enums;

/// <summary>Siparişin geldiği kanal. Vitrinden verilen sipariş <see cref="Site"/>; diğerlerini yönetici elle girer.</summary>
public enum OrderSource
{
    Site = 1,
    WhatsApp = 2,
    Instagram = 3,
    Telefon = 4,
    Magaza = 5
}
