using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;

namespace HerYerde.Business.Abstract;

public interface IProductTransferService
{
    /// <summary>Silinmemiş her ürün için bir ürün satırı ve her varyantı için bir varyant satırı, ürün kimliğine göre.</summary>
    Task<List<ProductSheetRow>> ExportAsync(CancellationToken cancellationToken = default);

    /// <summary>Hiçbir şey yazmadan her satırın ne olacağını söyler: yeni, güncellenecek ya da hata (nedeniyle).</summary>
    Task<ImportPreview> PreviewAsync(IReadOnlyList<ProductSheetRow> rows, CancellationToken cancellationToken = default);

    /// <summary>Önizlemeyi yeniden kurar; tek hatalı satır varsa 400 ve hiçbir yazım yok. Değilse hepsi tek işlemde yazılır.</summary>
    Task<(HttpStatusCode, IDataResult<ImportPreview>)> ApplyAsync(IReadOnlyList<ProductSheetRow> rows, CancellationToken cancellationToken = default);
}
