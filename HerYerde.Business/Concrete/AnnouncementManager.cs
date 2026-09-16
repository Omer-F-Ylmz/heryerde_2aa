using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class AnnouncementManager : IAnnouncementService
{
    /// <summary>Yönetim listesinde gösterilen en yeni kayıt sayısı.</summary>
    public const int Window = 50;

    private readonly IAnnouncementDal _announcementDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public AnnouncementManager(IAnnouncementDal announcementDal, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _announcementDal = announcementDal;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Şerit her sayfanın üstünde çizilir: tek satırlık, indeksli bir sorgu (ix_announcement_is_active_starts_at_ends_at).</summary>
    public Task<Announcement?> CurrentAsync(CancellationToken cancellationToken = default)
        => _announcementDal.CurrentAsync(_clock.GetUtcNow().UtcDateTime, cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<List<Announcement>>)> GetForAdminAsync(CancellationToken cancellationToken = default)
        => (HttpStatusCode.OK, new SuccessDataResult<List<Announcement>>(await _announcementDal.GetLatestAsync(Window, cancellationToken)));

    public async Task<(HttpStatusCode, IDataResult<Announcement>)> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _announcementDal.GetAsync(a => a.Id == id, cancellationToken) is { } found
            ? (HttpStatusCode.OK, new SuccessDataResult<Announcement>(found))
            : (HttpStatusCode.NotFound, new ErrorDataResult<Announcement>("Duyuru bulunamadı."));

    public async Task<(HttpStatusCode, IDataResult<Announcement>)> SaveAsync(Announcement announcement, CancellationToken cancellationToken = default)
    {
        var text = announcement.Text?.Trim() ?? string.Empty;
        if (text.Length is 0 or > 200)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Announcement>("Duyuru metni zorunlu (en çok 200 karakter)."));
        }

        if (announcement.EndsAt <= announcement.StartsAt)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Announcement>("Bitiş tarihi başlangıçtan sonra olmalı."));
        }

        // Açık yönlendirme olmasın: şerit yalnız site içi bir yola gidebilir.
        var url = announcement.Url?.Trim();
        if (url is { Length: > 0 } && !(url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal)))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Announcement>("Bağlantı site içi bir yol olmalı (ör. /ev)."));
        }

        var stored = announcement.Id > 0
            ? await _announcementDal.GetTrackedAsync(a => a.Id == announcement.Id, cancellationToken)
            : null;
        if (announcement.Id > 0 && stored is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<Announcement>("Duyuru bulunamadı."));
        }

        var target = stored ?? new Announcement { CreatedAt = _clock.GetUtcNow().UtcDateTime };
        target.Text = text;
        target.Url = url is { Length: > 0 } ? url : null;
        target.StartsAt = announcement.StartsAt;
        target.EndsAt = announcement.EndsAt;
        target.Color = announcement.Color;
        target.IsActive = announcement.IsActive;

        if (stored is null)
        {
            await _announcementDal.AddAsync(target, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<Announcement>(target, "Duyuru kaydedildi."));
    }

    public async Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var stored = await _announcementDal.GetTrackedAsync(a => a.Id == id, cancellationToken);
        if (stored is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Duyuru bulunamadı."));
        }

        _announcementDal.Delete(stored);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Duyuru silindi."));
    }
}
