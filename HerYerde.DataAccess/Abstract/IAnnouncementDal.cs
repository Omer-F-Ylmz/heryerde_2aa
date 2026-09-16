using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IAnnouncementDal : IEntityRepository<Announcement>
{
    /// <summary>O an geçerli (etkin ve tarih aralığında) en yeni duyuru; yoksa null.</summary>
    Task<Announcement?> CurrentAsync(DateTime moment, CancellationToken cancellationToken = default);

    /// <summary>Yönetim listesi: en yeniden eskiye.</summary>
    Task<List<Announcement>> GetLatestAsync(int take, CancellationToken cancellationToken = default);
}
