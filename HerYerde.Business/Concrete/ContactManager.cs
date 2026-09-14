using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class ContactManager : IContactService
{
    public const int Window = 200;

    private readonly IContactMessageDal _messageDal;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ContactManager(
        IContactMessageDal messageDal,
        INotificationService notifications,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _messageDal = messageDal;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<(HttpStatusCode, IResult)> SendAsync(ContactMessage message, CancellationToken cancellationToken = default)
    {
        message.CreatedAt = _clock.GetUtcNow().UtcDateTime;
        message.ReadAt = null;
        await _messageDal.AddAsync(message, cancellationToken);
        await _notifications.QueueContactMessageAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Mesajınız alındı."));
    }

    public async Task<(HttpStatusCode, IDataResult<List<ContactMessage>>)> GetRecentAsync(CancellationToken cancellationToken = default)
        => (HttpStatusCode.OK, new SuccessDataResult<List<ContactMessage>>(await _messageDal.GetRecentAsync(Window, cancellationToken)));

    public async Task<(HttpStatusCode, IResult)> MarkReadAsync(int id, CancellationToken cancellationToken = default)
    {
        var message = await _messageDal.GetTrackedAsync(m => m.Id == id, cancellationToken);
        if (message is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Mesaj bulunamadı."));
        }

        message.ReadAt ??= _clock.GetUtcNow().UtcDateTime;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Mesaj okundu işaretlendi."));
    }

    public async Task<(HttpStatusCode, IResult)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var message = await _messageDal.GetTrackedAsync(m => m.Id == id, cancellationToken);
        if (message is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Mesaj bulunamadı."));
        }

        _messageDal.Delete(message);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Mesaj silindi."));
    }
}
