using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Core.DataAccess;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;
using Microsoft.AspNetCore.Identity;

namespace HerYerde.Business.Concrete;

public class AdminAuthManager : IAdminAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private const string CredentialsMessage = "E-posta ya da parola hatalı.";
    private const int MinPasswordLength = 10;

    /// <summary>Bilinmeyen e-postada da doğrulanan sabit damga; böylece yanıt süresi hesabı ele vermez.</summary>
    private static readonly AdminUser DecoyUser = new() { Email = "decoy@heryerde.invalid" };
    private static readonly string DecoyHash = new PasswordHasher<AdminUser>()
        .HashPassword(DecoyUser, "decoy-parola-dogrulama-icin");

    private readonly IAdminUserDal _adminUserDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public AdminAuthManager(IAdminUserDal adminUserDal, IUnitOfWork unitOfWork)
    {
        _adminUserDal = adminUserDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<(HttpStatusCode, IDataResult<AdminUser>)> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var admin = await _adminUserDal.GetTrackedAsync(a => a.Email == email, cancellationToken);
        if (admin is null)
        {
            _passwordHasher.VerifyHashedPassword(DecoyUser, DecoyHash, password);
            return (HttpStatusCode.Unauthorized, new ErrorDataResult<AdminUser>(CredentialsMessage));
        }

        if (admin.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            // Kilitli hesap hatalı girişten ayırt edilemez: ne mesaj ne durum kodu bilgi sızdırır.
            return (HttpStatusCode.Unauthorized, new ErrorDataResult<AdminUser>(CredentialsMessage));
        }

        if (_passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            admin.FailedAttempts++;
            if (admin.FailedAttempts >= MaxFailedAttempts)
            {
                admin.LockedUntil = DateTime.UtcNow.Add(LockDuration);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return (HttpStatusCode.Unauthorized, new ErrorDataResult<AdminUser>(CredentialsMessage));
        }

        admin.FailedAttempts = 0;
        admin.LockedUntil = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<AdminUser>(admin));
    }

    public async Task<(HttpStatusCode, IDataResult<AdminUser>)> ChangePasswordAsync(
        int adminId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (PasswordProblem(newPassword) is { } problem)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<AdminUser>(problem));
        }

        var admin = await _adminUserDal.GetTrackedAsync(a => a.Id == adminId, cancellationToken);
        if (admin is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<AdminUser>("Yönetici bulunamadı."));
        }

        if (_passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<AdminUser>("Mevcut parola hatalı."));
        }

        admin.PasswordHash = _passwordHasher.HashPassword(admin, newPassword);
        // Damga ilerleyince diğer tarayıcılardaki çerezler bir sonraki istekte düşer.
        admin.PasswordChangedAt = DateTime.UtcNow;
        admin.FailedAttempts = 0;
        admin.LockedUntil = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<AdminUser>(admin, "Parola değiştirildi."));
    }

    public async Task<bool> StampIsCurrentAsync(int adminId, long stamp, CancellationToken cancellationToken = default)
        => await _adminUserDal.GetAsync(a => a.Id == adminId, cancellationToken) is { } admin
           && admin.PasswordChangedAt.Ticks == stamp;

    /// <summary>En az 10 karakter ve en az bir rakam.</summary>
    private static string? PasswordProblem(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            return $"Yeni parola en az {MinPasswordLength} karakter olmalı.";
        }

        return password.Any(char.IsDigit) ? null : "Yeni parola en az bir rakam içermeli.";
    }

    public async Task<(HttpStatusCode, IResult)> EnsureSeedAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (await _adminUserDal.GetAsync(a => a.Email == email, cancellationToken) is not null)
        {
            return (HttpStatusCode.OK, new SuccessResult("Yönetici zaten var."));
        }

        var admin = new AdminUser { Email = email, PasswordChangedAt = DateTime.UtcNow };
        admin.PasswordHash = _passwordHasher.HashPassword(admin, password);

        await _adminUserDal.AddAsync(admin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Yönetici oluşturuldu."));
    }
}
