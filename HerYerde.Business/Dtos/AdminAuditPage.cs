using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Dtos;

/// <summary>Denetim listesinin bir sayfası; Page aralığa çekilmiş sayfa numarasıdır.</summary>
public sealed record AdminAuditPage(List<AdminAuditLog> Items, int Page, int TotalPages);
