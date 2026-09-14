using HerYerde.Core.DataAccess;
using HerYerde.Entities.Concrete;

namespace HerYerde.DataAccess.Abstract;

public interface IContactMessageDal : IEntityRepository<ContactMessage>
{
    /// <summary>Yönetim listesi: en yeniden eskiye, en çok <paramref name="take"/> kayıt.</summary>
    Task<List<ContactMessage>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
