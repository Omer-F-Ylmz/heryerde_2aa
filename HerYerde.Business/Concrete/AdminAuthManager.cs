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
            return (HttpStatusCode.Unauthorized, new ErrorDataResult<AdminUser>(CredentialsMessage));
        }

        if (admin.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            return (HttpStatusCode.Locked, new ErrorDataResult<AdminUser>("Hesap 5 hatalı denemeden sonra 15 dakika kilitlendi."));
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

    public async Task<(HttpStatusCode, IResult)> EnsureSeedAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (await _adminUserDal.GetAsync(a => a.Email == email, cancellationToken) is not null)
        {
            return (HttpStatusCode.OK, new SuccessResult("Yönetici zaten var."));
        }

        var admin = new AdminUser { Email = email };
        admin.PasswordHash = _passwordHasher.HashPassword(admin, password);

        await _adminUserDal.AddAsync(admin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.Created, new SuccessResult("Yönetici oluşturuldu."));
    }
}
