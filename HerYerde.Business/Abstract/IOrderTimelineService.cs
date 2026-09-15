using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;

namespace HerYerde.Business.Abstract;

public interface IOrderTimelineService
{
    /// <summary>Boş ya da 1000 karakteri aşan not 400; sipariş yoksa 404.</summary>
    Task<(HttpStatusCode, IResult)> AddNoteAsync(int orderId, int adminId, string text, CancellationToken cancellationToken = default);

    /// <summary>Sipariş, postaları, denetim izi, iade talepleri ve notlar eskiden yeniye.</summary>
    Task<List<TimelineEntry>> GetAsync(int orderId, CancellationToken cancellationToken = default);
}
