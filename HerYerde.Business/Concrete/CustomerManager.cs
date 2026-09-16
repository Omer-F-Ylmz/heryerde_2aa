using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Business.Concrete;

public class CustomerManager : ICustomerService
{
    public const int MinPasswordLength = 10;
    public static readonly TimeSpan VerifyLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan LoginLinkLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(30);

    public const string SentMessage = "E-postanıza bir bağlantı gönderdik. Bağlantıyla hesabınızı doğrulayıp giriş yapabilirsiniz.";
    public const string LoginLinkMessage = "E-posta kayıtlıysa giriş bağlantısı gönderildi; bağlantı 15 dakika geçerli.";
    public const string ResetMessage = "E-posta kayıtlıysa parola sıfırlama bağlantısı gönderildi; bağlantı 30 dakika geçerli.";
    public const string CredentialsMessage = "E-posta ya da parola hatalı. Hesabınızı yeni açtıysanız önce e-postanızdaki doğrulama bağlantısını kullanın; parolasız hesapta giriş bağlantısı isteyin.";
    private const string LinkMessage = "Bağlantı geçersiz ya da süresi dolmuş; yeni bağlantı isteyin.";
    private const string PhoneMessage = "Geçerli bir cep telefonu yazın (05XX XXX XX XX).";

    /// <summary>Bilinmeyen e-postada da parola doğrulanır; yanıt süresi hesabın varlığını ele vermez.</summary>
    private static readonly Customer DecoyCustomer = new() { Email = "decoy@heryerde.invalid" };
    private static readonly string DecoyHash = new PasswordHasher<Customer>().HashPassword(DecoyCustomer, "decoy-parola-dogrulama-icin");

    private readonly ICustomerDal _customerDal;
    private readonly ICustomerAddressDal _addressDal;
    private readonly ICustomerFavoriteDal _favoriteDal;
    private readonly IProductDal _productDal;
    private readonly IOrderDal _orderDal;
    private readonly IProductReviewDal _reviewDal;
    private readonly IGiftRegistryDal _registryDal;
    private readonly IContactMessageDal _messageDal;
    private readonly IOrderService _orders;
    private readonly IGiftRegistryService _registries;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly PasswordHasher<Customer> _passwordHasher = new();

    public CustomerManager(
        ICustomerDal customerDal,
        ICustomerAddressDal addressDal,
        ICustomerFavoriteDal favoriteDal,
        IProductDal productDal,
        IOrderDal orderDal,
        IProductReviewDal reviewDal,
        IGiftRegistryDal registryDal,
        IContactMessageDal messageDal,
        IOrderService orders,
        IGiftRegistryService registries,
        INotificationService notifications,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _customerDal = customerDal;
        _addressDal = addressDal;
        _favoriteDal = favoriteDal;
        _productDal = productDal;
        _orderDal = orderDal;
        _reviewDal = reviewDal;
        _registryDal = registryDal;
        _messageDal = messageDal;
        _orders = orders;
        _registries = registries;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    /// <summary>Küçük harfe indirilmiş geçerli adres; değilse null.</summary>
    public static string? NormalizeEmail(string? email)
    {
        var text = email?.Trim() ?? string.Empty;
        return text.Length is > 0 and <= 200 && MailAddress.TryCreate(text, out var mail) && mail.Address == text
            ? text.ToLowerInvariant()
            : null;
    }

    public async Task<(HttpStatusCode, IResult)> SignUpAsync(CustomerSignUp draft, CancellationToken cancellationToken = default)
    {
        if (NormalizeEmail(draft.Email) is not { } email)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Geçerli bir e-posta adresi yazın."));
        }

        if (!string.IsNullOrEmpty(draft.Password) && draft.Password.Length < MinPasswordLength)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult($"Parola en az {MinPasswordLength} karakter olmalı; parolasız üyelik için boş bırakın."));
        }

        if (!draft.KvkkConsent)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Üye olmak için aydınlatma metnini okuduğunuzu onaylayın."));
        }

        var now = Now;
        var existing = await _customerDal.GetTrackedAsync(c => c.Email == email, cancellationToken);
        if (existing is null)
        {
            var customer = new Customer
            {
                Email = email,
                KvkkConsentAt = now,
                LegalVersion = LegalDocs.Version,
                MarketingConsent = draft.MarketingConsent,
                MarketingConsentAt = draft.MarketingConsent ? now : null,
                CreatedAt = now,
                SessionStamp = DateTime.UtcNow
            };
            if (!string.IsNullOrEmpty(draft.Password))
            {
                customer.PasswordHash = _passwordHasher.HashPassword(customer, draft.Password);
            }

            await _customerDal.AddAsync(customer, cancellationToken);
            await _notifications.QueueCustomerVerifyAsync(email, IssueVerifyToken(customer, now), cancellationToken);
        }
        else if (existing.EmailVerifiedAt is null)
        {
            // Parola değişmez: e-postanın sahibi olduğu kanıtlanmamış biri kayıtlı parolayı ezemez.
            await _notifications.QueueCustomerVerifyAsync(email, IssueVerifyToken(existing, now), cancellationToken);
        }
        else
        {
            await _notifications.QueueCustomerLoginLinkAsync(email, IssueLoginToken(existing, now), cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Aynı adrese eşzamanlı ikinci kayıt: ilk kayıt postasını aldı, yanıt yine aynı.
            if (await _customerDal.GetAsync(c => c.Email == email, cancellationToken) is null)
            {
                throw;
            }
        }

        return (HttpStatusCode.OK, new SuccessResult(SentMessage));
    }

    public async Task<Customer?> PeekVerifyAsync(string token, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(token)
            ? null
            : await _customerDal.GetAsync(c => c.VerifyTokenHash == Sha256(token) && c.VerifyTokenExpiresAt > Now, cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<Customer>)> VerifyEmailAsync(string token, string? password, CancellationToken cancellationToken = default)
    {
        var hash = string.IsNullOrEmpty(token) ? string.Empty : Sha256(token);
        var customer = hash.Length == 0 ? null : await _customerDal.GetTrackedAsync(c => c.VerifyTokenHash == hash, cancellationToken);
        if (customer is null || customer.VerifyTokenExpiresAt is not { } expires || expires <= Now)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Customer>(LinkMessage));
        }

        if (customer.PasswordHash is not null
            && _passwordHasher.VerifyHashedPassword(customer, customer.PasswordHash, password ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Customer>(
                "Parola hatalı. Üye olurken belirlediğiniz parolayı yazın; hatırlamıyorsanız giriş bağlantısı isteyin."));
        }

        customer.VerifyTokenHash = null;
        customer.VerifyTokenExpiresAt = null;
        await MarkVerifiedAsync(customer, cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<Customer>(customer, "E-postanız doğrulandı."));
    }

    public async Task<(HttpStatusCode, IDataResult<Customer>)> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        var customer = normalized is null ? null : await _customerDal.GetAsync(c => c.Email == normalized, cancellationToken);
        if (customer is not { PasswordHash: { } passwordHash, EmailVerifiedAt: not null })
        {
            _passwordHasher.VerifyHashedPassword(DecoyCustomer, DecoyHash, password ?? string.Empty);
            return (HttpStatusCode.Unauthorized, new ErrorDataResult<Customer>(CredentialsMessage));
        }

        return _passwordHasher.VerifyHashedPassword(customer, passwordHash, password ?? string.Empty) == PasswordVerificationResult.Failed
            ? (HttpStatusCode.Unauthorized, new ErrorDataResult<Customer>(CredentialsMessage))
            : (HttpStatusCode.OK, new SuccessDataResult<Customer>(customer));
    }

    public async Task<(HttpStatusCode, IResult)> RequestLoginLinkAsync(string email, CancellationToken cancellationToken = default)
    {
        if (NormalizeEmail(email) is { } normalized
            && await _customerDal.GetTrackedAsync(c => c.Email == normalized, cancellationToken) is { } customer)
        {
            await _notifications.QueueCustomerLoginLinkAsync(customer.Email, IssueLoginToken(customer, Now), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (HttpStatusCode.OK, new SuccessResult(LoginLinkMessage));
    }

    public async Task<(HttpStatusCode, IDataResult<Customer>)> SignInWithLinkAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = string.IsNullOrEmpty(token) ? string.Empty : Sha256(token);
        var customer = hash.Length == 0 ? null : await _customerDal.GetTrackedAsync(c => c.LoginTokenHash == hash, cancellationToken);
        if (customer is null || customer.LoginTokenExpiresAt is not { } expires || expires <= Now)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Customer>(LinkMessage));
        }

        customer.LoginTokenHash = null;
        customer.LoginTokenExpiresAt = null;
        if (customer.EmailVerifiedAt is null)
        {
            // Kayıttaki parolayı e-postanın sahibi koymamış olabilir (başkası bu adresle üye olmuş): bağlantıyla
            // doğrulanan hesapta kanıtlanmamış parola geçersizleşir; sahibi dilerse yeni parola belirler.
            customer.PasswordHash = null;
        }

        await MarkVerifiedAsync(customer, cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<Customer>(customer));
    }

    public async Task<(HttpStatusCode, IResult)> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        if (NormalizeEmail(email) is { } normalized
            && await _customerDal.GetTrackedAsync(c => c.Email == normalized, cancellationToken) is { } customer)
        {
            var token = NewToken();
            customer.ResetTokenHash = Sha256(token);
            customer.ResetTokenExpiresAt = Now + ResetLifetime;
            await _notifications.QueueCustomerPasswordResetAsync(customer.Email, token, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (HttpStatusCode.OK, new SuccessResult(ResetMessage));
    }

    public async Task<(HttpStatusCode, IResult)> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var hash = string.IsNullOrEmpty(token) ? string.Empty : Sha256(token);
        var customer = hash.Length == 0 ? null : await _customerDal.GetTrackedAsync(c => c.ResetTokenHash == hash, cancellationToken);
        if (customer is null || customer.ResetTokenExpiresAt is not { } expires || expires <= Now)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(LinkMessage));
        }

        if (PasswordProblem(newPassword) is { } problem)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(problem));
        }

        SetPassword(customer, newPassword);
        await MarkVerifiedAsync(customer, cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Parolanız değişti; yeni parolayla giriş yapın."));
    }

    public async Task<(HttpStatusCode, IDataResult<Customer>)> ChangePasswordAsync(
        int customerId,
        string? currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customerDal.GetTrackedAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<Customer>("Hesap bulunamadı."));
        }

        if (customer.PasswordHash is not null
            && _passwordHasher.VerifyHashedPassword(customer, customer.PasswordHash, currentPassword ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Customer>("Mevcut parola hatalı."));
        }

        if (PasswordProblem(newPassword) is { } problem)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Customer>(problem));
        }

        SetPassword(customer, newPassword);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<Customer>(customer, "Parolanız kaydedildi; diğer cihazlardaki oturumlar kapandı."));
    }

    public async Task<(HttpStatusCode, IDataResult<Customer>)> RevokeSessionsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _customerDal.GetTrackedAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<Customer>("Hesap bulunamadı."));
        }

        customer.SessionStamp = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<Customer>(customer, "Diğer tüm cihazlardaki oturumlar kapatıldı."));
    }

    public async Task<bool> StampIsCurrentAsync(int customerId, long stamp, CancellationToken cancellationToken = default)
        => await _customerDal.GetAsync(c => c.Id == customerId, cancellationToken) is { } customer
           && customer.SessionStamp.Ticks == stamp;

    public Task<Customer?> GetAsync(int customerId, CancellationToken cancellationToken = default)
        => _customerDal.GetAsync(c => c.Id == customerId, cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<Customer>)> UpdateProfileAsync(int customerId, CustomerProfile profile, CancellationToken cancellationToken = default)
    {
        var customer = await _customerDal.GetTrackedAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<Customer>("Hesap bulunamadı."));
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(profile.Phone) && !PhoneRules.TryNormalize(profile.Phone, out phone!))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<Customer>(PhoneMessage));
        }

        customer.FullName = string.IsNullOrWhiteSpace(profile.FullName) ? null : profile.FullName.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(profile.Phone) ? null : phone;
        if (customer.MarketingConsent != profile.MarketingConsent)
        {
            customer.MarketingConsent = profile.MarketingConsent;
            customer.MarketingConsentAt = Now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<Customer>(customer, "Bilgileriniz kaydedildi."));
    }

    public async Task<List<CustomerAddress>> GetAddressesAsync(int customerId, CancellationToken cancellationToken = default)
        => (await _addressDal.GetListAsync(a => a.CustomerId == customerId, cancellationToken))
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToList();

    public async Task<(HttpStatusCode, IResult)> SaveAddressAsync(int customerId, CustomerAddress address, CancellationToken cancellationToken = default)
    {
        if (!PhoneRules.TryNormalize(address.Phone, out var phone))
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(PhoneMessage));
        }

        var existing = await _addressDal.GetListAsync(a => a.CustomerId == customerId, cancellationToken);
        CustomerAddress target;
        if (address.Id == 0)
        {
            target = new CustomerAddress { CustomerId = customerId, CreatedAt = Now };
            await _addressDal.AddAsync(target, cancellationToken);
        }
        else if (await _addressDal.GetTrackedAsync(a => a.Id == address.Id && a.CustomerId == customerId, cancellationToken) is { } tracked)
        {
            target = tracked;
        }
        else
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Adres bulunamadı."));
        }

        target.Title = address.Title.Trim();
        target.FullName = address.FullName.Trim();
        target.Phone = phone;
        target.Address = address.Address.Trim();
        target.City = address.City.Trim();
        target.District = address.District.Trim();
        // İlk adres kendiliğinden varsayılandır; varsayılan işaretlenen adres öncekinin yerini alır.
        target.IsDefault = address.IsDefault || existing.All(a => a.Id == address.Id || !a.IsDefault);
        if (target.IsDefault)
        {
            foreach (var other in existing.Where(a => a.IsDefault && a.Id != address.Id))
            {
                (await _addressDal.GetTrackedAsync(a => a.Id == other.Id, cancellationToken))!.IsDefault = false;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return address.Id == 0
            ? (HttpStatusCode.Created, new SuccessResult("Adres eklendi."))
            : (HttpStatusCode.OK, new SuccessResult("Adres kaydedildi."));
    }

    public async Task<(HttpStatusCode, IResult)> DeleteAddressAsync(int customerId, int addressId, CancellationToken cancellationToken = default)
    {
        var address = await _addressDal.GetTrackedAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken);
        if (address is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Adres bulunamadı."));
        }

        _addressDal.Delete(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Adres silindi."));
    }

    public Task<HashSet<int>> GetFavoriteIdsAsync(int customerId, CancellationToken cancellationToken = default)
        => _favoriteDal.ProductIdsAsync(customerId, cancellationToken);

    public async Task<(HttpStatusCode, IDataResult<bool>)> ToggleFavoriteAsync(int customerId, int productId, CancellationToken cancellationToken = default)
    {
        if (await _favoriteDal.GetTrackedAsync(f => f.CustomerId == customerId && f.ProductId == productId, cancellationToken) is { } favorite)
        {
            _favoriteDal.Delete(favorite);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return (HttpStatusCode.OK, new SuccessDataResult<bool>(false, "Favorilerden çıkarıldı."));
        }

        if (await _productDal.GetAsync(p => p.Id == productId && p.IsActive, cancellationToken) is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<bool>("Ürün bulunamadı."));
        }

        await _favoriteDal.AddAsync(new CustomerFavorite { CustomerId = customerId, ProductId = productId, CreatedAt = Now }, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Çift tıklamada ikinci istek: kayıt zaten var.
            if (await _favoriteDal.GetAsync(f => f.CustomerId == customerId && f.ProductId == productId, cancellationToken) is null)
            {
                throw;
            }
        }

        return (HttpStatusCode.OK, new SuccessDataResult<bool>(true, "Favorilere eklendi."));
    }

    public async Task<List<Product>> GetFavoriteProductsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var favorites = (await _favoriteDal.GetListAsync(f => f.CustomerId == customerId, cancellationToken))
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .Select(f => f.ProductId)
            .ToList();
        var products = await _productDal.GetListAsync(p => favorites.Contains(p.Id) && p.IsActive, cancellationToken);
        return products.OrderBy(p => favorites.IndexOf(p.Id)).ToList();
    }

    public async Task<List<Order>> GetOrdersAsync(int customerId, CancellationToken cancellationToken = default)
        => (await _orderDal.GetListAsync(o => o.CustomerId == customerId, cancellationToken))
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .ToList();

    public Task<List<ReviewRow>> GetReviewsAsync(int customerId, CancellationToken cancellationToken = default)
        => _reviewDal.GetByCustomerAsync(customerId, cancellationToken);

    public async Task<List<GiftRegistry>> GetRegistriesAsync(int customerId, CancellationToken cancellationToken = default)
        => (await _registryDal.GetListAsync(r => r.CustomerId == customerId, cancellationToken))
            .OrderByDescending(r => r.EventDate)
            .ToList();

    public async Task<(HttpStatusCode, IDataResult<CustomerDeletion>)> DeleteAccountAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _customerDal.GetTrackedAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<CustomerDeletion>("Hesap bulunamadı."));
        }

        var orders = await _orderDal.GetListAsync(o => o.CustomerId == customerId, cancellationToken);
        var open = orders.Where(o => !OrderRules.CanAnonymize(o.Status)).Select(o => o.OrderNo).ToList();
        if (open.Count > 0)
        {
            // Açık siparişte teslimat ve teyit için veri gerekir; hesap yarım silinmesin diye hiçbir şeye dokunulmaz.
            return (HttpStatusCode.Conflict, new ErrorDataResult<CustomerDeletion>(
                $"Açık siparişiniz var ({string.Join(", ", open)}). Teslim edildikten ya da iptal edildikten sonra hesabınızı silebilirsiniz."));
        }

        var files = new List<string>();
        foreach (var order in orders)
        {
            var (_, result) = await _orders.AnonymizeAsync(order.Id, cancellationToken);
            files.AddRange(result.Data ?? []);
        }

        foreach (var review in await _reviewDal.GetListAsync(r => r.CustomerId == customerId, cancellationToken))
        {
            var tracked = (await _reviewDal.GetTrackedAsync(r => r.Id == review.Id, cancellationToken))!;
            tracked.Name = PersonalDataMask.Name(tracked.Name);
            tracked.CustomerId = null;
        }

        foreach (var message in (await _messageDal.GetListAsync(cancellationToken: cancellationToken))
                     .Where(m => KvkkManager.Subject(m.Contact) == customer.Email))
        {
            _messageDal.Delete((await _messageDal.GetTrackedAsync(m => m.Id == message.Id, cancellationToken))!);
        }

        foreach (var registry in await _registryDal.GetListAsync(r => r.CustomerId == customerId, cancellationToken))
        {
            await _registries.DeleteAsync(registry.Id, cancellationToken);
        }

        // Adresler ve favoriler yabancı anahtarla birlikte silinir; sipariş, yorum ve listelerdeki bağ boşalır.
        _customerDal.Delete(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<CustomerDeletion>(new CustomerDeletion(orders, files), "Hesabınız silindi."));
    }

    /// <summary>İlk doğrulamada aynı e-postalı misafir siparişleri ve çeyiz listeleri üyeye bağlanır.</summary>
    private async Task MarkVerifiedAsync(Customer customer, CancellationToken cancellationToken)
    {
        var first = customer.EmailVerifiedAt is null;
        customer.EmailVerifiedAt ??= Now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (first)
        {
            await _customerDal.LinkGuestRecordsAsync(customer.Id, customer.Email, cancellationToken);
        }
    }

    private string IssueVerifyToken(Customer customer, DateTime now)
    {
        var token = NewToken();
        customer.VerifyTokenHash = Sha256(token);
        customer.VerifyTokenExpiresAt = now + VerifyLifetime;
        return token;
    }

    private string IssueLoginToken(Customer customer, DateTime now)
    {
        var token = NewToken();
        customer.LoginTokenHash = Sha256(token);
        customer.LoginTokenExpiresAt = now + LoginLinkLifetime;
        return token;
    }

    /// <summary>Damga ilerler: parola değişince diğer tarayıcılardaki oturumlar düşer, açık bağlantılar geçersizleşir.</summary>
    private void SetPassword(Customer customer, string password)
    {
        customer.PasswordHash = _passwordHasher.HashPassword(customer, password);
        customer.SessionStamp = DateTime.UtcNow;
        customer.ResetTokenHash = null;
        customer.ResetTokenExpiresAt = null;
        customer.LoginTokenHash = null;
        customer.LoginTokenExpiresAt = null;
    }

    private static string? PasswordProblem(string password)
        => string.IsNullOrEmpty(password) || password.Length < MinPasswordLength
            ? $"Parola en az {MinPasswordLength} karakter olmalı."
            : null;

    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
