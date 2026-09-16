using System.Net;
using System.Net.Mail;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Concrete;

public class KvkkManager : IKvkkService
{
    /// <summary>KVKK m. 13/2: başvuru en geç 30 gün içinde sonuçlandırılır.</summary>
    public const int ResponseDays = 30;

    private const string InvalidQuery = "Kişiyi bulmak için cep telefonu (05XX XXX XX XX) ya da e-posta adresi yazın.";

    private readonly IOrderDal _orderDal;
    private readonly IOrderItemDal _orderItemDal;
    private readonly IPaymentDal _paymentDal;
    private readonly IPaymentNoticeDal _noticeDal;
    private readonly IReturnRequestDal _returnDal;
    private readonly IReturnRequestItemDal _returnItemDal;
    private readonly IContactMessageDal _messageDal;
    private readonly IProductReviewDal _reviewDal;
    private readonly IKvkkRequestDal _requestDal;
    private readonly ICustomerDal _customerDal;
    private readonly ICustomerAddressDal _addressDal;
    private readonly ICustomerFavoriteDal _favoriteDal;
    private readonly IOrderService _orders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public KvkkManager(
        IOrderDal orderDal,
        IOrderItemDal orderItemDal,
        IPaymentDal paymentDal,
        IPaymentNoticeDal noticeDal,
        IReturnRequestDal returnDal,
        IReturnRequestItemDal returnItemDal,
        IContactMessageDal messageDal,
        IProductReviewDal reviewDal,
        IKvkkRequestDal requestDal,
        ICustomerDal customerDal,
        ICustomerAddressDal addressDal,
        ICustomerFavoriteDal favoriteDal,
        IOrderService orders,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _orderDal = orderDal;
        _orderItemDal = orderItemDal;
        _paymentDal = paymentDal;
        _noticeDal = noticeDal;
        _returnDal = returnDal;
        _returnItemDal = returnItemDal;
        _messageDal = messageDal;
        _reviewDal = reviewDal;
        _requestDal = requestDal;
        _customerDal = customerDal;
        _addressDal = addressDal;
        _favoriteDal = favoriteDal;
        _orders = orders;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Telefon tek biçime ("05XXXXXXXXX"), e-posta küçük harfe indirgenir; ikisi de değilse null.</summary>
    public static string? Subject(string? query)
    {
        if (PhoneRules.TryNormalize(query, out var phone))
        {
            return phone;
        }

        var text = query?.Trim() ?? string.Empty;
        return text.Contains('@') && MailAddress.TryCreate(text, out var mail) && mail.Address == text ? text.ToLowerInvariant() : null;
    }

    /// <summary>Denetim izine yazılacak maskeli kişi ("05*******82", "a***@***").</summary>
    public static string Masked(string subject)
        => subject.Contains('@') ? PersonalDataMask.Email(subject) : PersonalDataMask.Phone(subject);

    public async Task<(HttpStatusCode, IDataResult<KvkkPerson>)> FindAsync(string query, CancellationToken cancellationToken = default)
    {
        if (Subject(query) is not { } subject)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<KvkkPerson>(InvalidQuery));
        }

        var byEmail = subject.Contains('@');
        // Üye hesabı e-postasıyla ya da hesaptaki telefonla bulunur; hesaba bağlı siparişler başka e-postayla verilmiş olsa da gelir.
        var accounts = byEmail
            ? await _customerDal.GetListAsync(c => c.Email == subject, cancellationToken)
            : await _customerDal.GetListAsync(c => c.Phone == subject, cancellationToken);
        var accountIds = accounts.Select(c => (int?)c.Id).ToList();
        var orders = byEmail
            ? await _orderDal.GetListAsync(o => (o.Email != null && o.Email.ToLower() == subject) || accountIds.Contains(o.CustomerId), cancellationToken)
            : await _orderDal.GetListAsync(o => o.Phone == subject || accountIds.Contains(o.CustomerId), cancellationToken);
        var ids = orders.Select(o => o.Id).ToList();
        var numbers = orders.Select(o => o.OrderNo).ToList();
        var returns = ids.Count == 0 ? [] : await _returnDal.GetListAsync(r => ids.Contains(r.OrderId), cancellationToken);
        var returnIds = returns.Select(r => r.Id).ToList();

        // Kişi siparişlerindeki telefon ve e-postayla genişler: e-postayla aranan kişinin telefonla yazdığı mesaj da bulunur.
        // İletişim alanı serbest metin ("0 (542) 497-0982"): aynı indirgemeyle bellekte eşleşir.
        var identities = orders.Select(o => o.Phone).Concat(orders.Select(o => o.Email?.ToLowerInvariant())).OfType<string>().Append(subject).ToHashSet();
        var messages = (await _messageDal.GetListAsync(cancellationToken: cancellationToken))
            .Where(m => Subject(m.Contact) is { } contact && identities.Contains(contact))
            .ToList();

        return (HttpStatusCode.OK, new SuccessDataResult<KvkkPerson>(new KvkkPerson(
            subject,
            orders.OrderBy(o => o.Id).ToList(),
            ids.Count == 0 ? [] : await _orderItemDal.GetListAsync(i => ids.Contains(i.OrderId), cancellationToken),
            ids.Count == 0 ? [] : await _paymentDal.GetListAsync(p => ids.Contains(p.OrderId), cancellationToken),
            ids.Count == 0 ? [] : await _noticeDal.GetListAsync(n => ids.Contains(n.OrderId), cancellationToken),
            returns,
            returnIds.Count == 0 ? [] : await _returnItemDal.GetListAsync(i => returnIds.Contains(i.ReturnRequestId), cancellationToken),
            messages,
            await _reviewDal.GetListAsync(r => (r.OrderNo != null && numbers.Contains(r.OrderNo)) || accountIds.Contains(r.CustomerId), cancellationToken),
            accounts.FirstOrDefault() is { } account
                ? new KvkkAccount(
                    account.Email,
                    account.FullName,
                    account.Phone,
                    account.CreatedAt,
                    account.EmailVerifiedAt,
                    account.KvkkConsentAt,
                    account.LegalVersion,
                    account.MarketingConsent,
                    account.MarketingConsentAt,
                    account.PasswordHash is not null,
                    (await _favoriteDal.ProductIdsAsync(account.Id, cancellationToken)).Order().ToList())
                : null,
            accountIds.Count == 0 ? [] : await _addressDal.GetListAsync(a => accountIds.Contains(a.CustomerId), cancellationToken))));
    }

    public async Task<(HttpStatusCode, IDataResult<KvkkAnonymizeResult>)> AnonymizeAllAsync(string query, CancellationToken cancellationToken = default)
    {
        var (status, found) = await FindAsync(query, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return (status, new ErrorDataResult<KvkkAnonymizeResult>(found.Message));
        }

        var person = found.Data!;
        var anonymized = new List<Order>();
        var skipped = new List<string>();
        var files = new List<string>();
        foreach (var order in person.Orders)
        {
            // Açık siparişte veri teslimat ve teyit için gerekir (yasal saklama); başvurana gerekçesi bildirilir.
            if (!OrderRules.CanAnonymize(order.Status))
            {
                skipped.Add(order.OrderNo);
                continue;
            }

            var (_, result) = await _orders.AnonymizeAsync(order.Id, cancellationToken);
            anonymized.Add(order);
            files.AddRange(result.Data ?? []);
        }

        foreach (var message in person.ContactMessages)
        {
            _messageDal.Delete((await _messageDal.GetTrackedAsync(m => m.Id == message.Id, cancellationToken))!);
        }

        foreach (var review in person.Reviews)
        {
            var tracked = (await _reviewDal.GetTrackedAsync(r => r.Id == review.Id, cancellationToken))!;
            tracked.Name = PersonalDataMask.Name(tracked.Name);
        }

        // Üye hesabı adresleri ve favorileriyle silinir (yabancı anahtar); açık siparişin hesap bağı boşalır, sipariş kalır.
        foreach (var account in await _customerDal.GetListAsync(c => person.Account != null && c.Email == person.Account.Email, cancellationToken))
        {
            _customerDal.Delete((await _customerDal.GetTrackedAsync(c => c.Id == account.Id, cancellationToken))!);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<KvkkAnonymizeResult>(
            new KvkkAnonymizeResult(anonymized, skipped, person.ContactMessages.Count, person.Reviews.Count, files),
            skipped.Count == 0
                ? "Kişinin tüm kayıtları anonimleştirildi."
                : $"Kapanmış kayıtlar anonimleştirildi; açık siparişler atlandı: {string.Join(", ", skipped)}."));
    }

    public async Task<(HttpStatusCode, IDataResult<KvkkRequest>)> OpenRequestAsync(string subject, DateTime receivedAt, CancellationToken cancellationToken = default)
    {
        if (Subject(subject) is not { } normalized)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<KvkkRequest>(InvalidQuery));
        }

        var request = new KvkkRequest { Subject = normalized, ReceivedAt = receivedAt };
        await _requestDal.AddAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessDataResult<KvkkRequest>(request, "Başvuru kaydedildi; 30 gün içinde yanıtlanmalı."));
    }

    public async Task<(HttpStatusCode, IResult)> CompleteRequestAsync(int id, CancellationToken cancellationToken = default)
    {
        var request = await _requestDal.GetTrackedAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Başvuru bulunamadı."));
        }

        request.CompletedAt ??= _clock.GetUtcNow().UtcDateTime;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Başvuru yanıtlandı olarak işaretlendi."));
    }

    public async Task<(HttpStatusCode, IDataResult<List<KvkkRequestRow>>)> GetRequestsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return (HttpStatusCode.OK, new SuccessDataResult<List<KvkkRequestRow>>((await _requestDal.GetListAsync(cancellationToken: cancellationToken))
            .OrderBy(r => r.CompletedAt is not null)
            .ThenBy(r => r.ReceivedAt)
            .Select(r => new KvkkRequestRow(r, r.CompletedAt is null ? ResponseDays - (int)Math.Floor((now - r.ReceivedAt).TotalDays) : null))
            .ToList()));
    }
}
