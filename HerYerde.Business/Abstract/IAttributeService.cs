using System.Net;
using HerYerde.Business.Rules;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

/// <summary>Ürün özellikleri ve kategori özellik şablonu.</summary>
public interface IAttributeService
{
    /// <summary>Sıraya göre.</summary>
    Task<List<ProductAttribute>> GetForProductAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Ürünün özelliklerini verilen sırayla değiştirir; boş liste hepsini siler.</summary>
    Task<(HttpStatusCode, IResult)> SetForProductAsync(int productId, IReadOnlyList<AttributePair> pairs, CancellationToken cancellationToken = default);

    /// <summary>Kategorinin şablon adları; kategorinin kendi şablonu yoksa üst kategorininki.</summary>
    Task<List<string>> GetTemplateAsync(int categoryId, CancellationToken cancellationToken = default);

    Task SetTemplateAsync(int categoryId, IReadOnlyList<string> names, CancellationToken cancellationToken = default);
}
