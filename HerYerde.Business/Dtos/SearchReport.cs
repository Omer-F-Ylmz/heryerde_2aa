using HerYerde.DataAccess.Abstract;

namespace HerYerde.Business.Dtos;

/// <summary>Yönetim arama raporu: sonuç bulunamayan terimler ve en çok arananlar, aranma sayısına göre.</summary>
public sealed record SearchReport(IReadOnlyList<SearchTermRow> ZeroResults, IReadOnlyList<SearchTermRow> Top);
