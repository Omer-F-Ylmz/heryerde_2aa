namespace HerYerde.Business.Dtos;

public sealed record BrandFacet(string Slug, string Name, int Count);

public sealed record AttributeValueFacet(string Value, int Count);

public sealed record AttributeFacet(string Name, IReadOnlyList<AttributeValueFacet> Values);

/// <summary>Süzgeç paneli: kapsamdaki markalar ve özellik değerleri, sayılarıyla; ada göre sıralı.</summary>
public sealed record ListingFacets(IReadOnlyList<BrandFacet> Brands, IReadOnlyList<AttributeFacet> Attributes, int InStock = 0, int Campaign = 0);
