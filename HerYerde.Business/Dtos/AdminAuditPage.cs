using HerYerde.DataAccess.Abstract;

namespace HerYerde.Business.Dtos;

/// <summary>Denetim listesinin bir sayfası; Page aralığa çekilmiş sayfa numarasıdır.</summary>
public sealed record AdminAuditPage(List<AdminAuditRow> Items, int Page, int TotalPages);
