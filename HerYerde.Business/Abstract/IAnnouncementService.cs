using System.Net;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

/// <summary>Site üstündeki duyuru şeridi ve yönetimi.</summary>
public interface IAnnouncementService
{
    /// <summary>O an geçerli duyuru; yoksa null. Vitrinin her sayfasında sorulur.</summary>
    Task<Announcement?> CurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>Yönetim listesi: en yeniden eskiye.</summary>
    Task<(HttpStatusCode, IDataResult<List<Announcement>>)> GetForAdminAsync(CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<Announcement>)> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Metin zorunlu, bitiş başlangıçtan sonra olmalı, bağlantı yalnız site içi yol olabilir.</summary>
    Task<(HttpStatusCode, IDataResult<Announcement>)> SaveAsync(Announcement announcement, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
