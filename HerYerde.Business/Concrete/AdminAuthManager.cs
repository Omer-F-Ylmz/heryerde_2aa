using System.Net;
using System.Security.Cryptography;
using System.Text;
using HerYerde.Business.Abstract;
using HerYerde.Business.Rules;
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
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(30);
    private const string CredentialsMessage = "E-posta ya da parola hatalı.";
    private const string CodeMessage = "Kod hatalı ya da süresi geçmiş.";
    private const int MinPasswordLength = 10;
    private const int RecoveryCodeCount = 8;

    /// <summary>Bilinmeyen e-postada da doğrulanan sabit damga; böylece yanıt süresi hesabı ele vermez.</summary>
    private static readonly AdminUser DecoyUser = new() { Email = "decoy@heryerde.invalid" };
    private static readonly string DecoyHash = new PasswordHasher<AdminUser>()
        .HashPassword(DecoyUser, "decoy-parola-dogrulama-icin");

    private readonly IAdminUserDal _adminUserDal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _clock;
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public AdminAuthManager(IAdminUserDal adminUserDal, IUnitOfWork unitOfWork, INotificationService notifications, TimeProvider clock)
    {
        _adminUserDal = adminUserDal;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _clock = clock;
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
            await FailAsync(admin, cancellationToken);
            return (HttpStatusCode.Unauthorized, new ErrorDataResult<AdminUser>(CredentialsMessage));
        }

        // İki adımlıda sayaç kod doğrulanınca sıfırlanır: parolayı bilen, yeniden girerek kod denemelerini sıfırlayamaz.
        if (!admin.TotpEnabled)
        {
            admin.FailedAttempts = 0;
            admin.LockedUntil = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (HttpStatusCode.OK, new SuccessDataResult<AdminUser>(admin));
    }

    public async Task<(HttpStatusCode, IDataResult<AdminUser>)> VerifySecondFactorAsync(int adminId, string code, CancellationToken cancellationToken = default)
    {
        var admin = await _adminUserDal.GetTrackedAsync(a => a.Id == adminId, cancellationToken);
        if (admin is not { TotpEnabled: true, TotpSecret: { } secret } || admin.LockedUntil > DateTime.UtcNow)
        {
            return (HttpStatusCode.Unauthorized, new ErrorDataResult<AdminUser>(CodeMessage));
        }

        var normalized = new string(code.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        var matched = normalized.Length == 6 && normalized.All(char.IsAsciiDigit)
            ? UseTotpStep(admin, Totp.MatchedStep(secret, normalized, _clock.GetUtcNow()))
            : UseRecoveryCode(admin, normalized);

        if (!matched)
        {
            await FailAsync(admin, cancellationToken);
            return (HttpStatusCode.BadRequest, new ErrorDataResult<AdminUser>(CodeMessage));
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

        SetPassword(admin, newPassword);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<AdminUser>(admin, "Parola değiştirildi."));
    }

    public async Task<(HttpStatusCode, IResult)> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        if (await _adminUserDal.GetTrackedAsync(a => a.Email == email, cancellationToken) is { } admin)
        {
            var token = Base64Url(RandomNumberGenerator.GetBytes(32));
            admin.ResetTokenHash = Sha256(token);
            admin.ResetTokenExpiresAt = _clock.GetUtcNow().UtcDateTime + ResetLifetime;
            await _notifications.QueueAdminPasswordResetAsync(admin.Email, token, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Kayıtlı olsun olmasın aynı yanıt: form hangi e-postanın yönetici olduğunu ele vermez.
        return (HttpStatusCode.OK, new SuccessResult("E-posta kayıtlıysa parola sıfırlama için bağlantı gönderildi."));
    }

    public async Task<(HttpStatusCode, IResult)> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var hash = string.IsNullOrEmpty(token) ? string.Empty : Sha256(token);
        var admin = hash.Length == 0 ? null : await _adminUserDal.GetTrackedAsync(a => a.ResetTokenHash == hash, cancellationToken);
        if (admin is null || admin.ResetTokenExpiresAt is not { } expires || expires <= _clock.GetUtcNow().UtcDateTime)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Bağlantı geçersiz ya da süresi dolmuş; yeni bağlantı isteyin."));
        }

        if (PasswordProblem(newPassword) is { } problem)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult(problem));
        }

        SetPassword(admin, newPassword);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("Parola değiştirildi; yeni parolayla giriş yapın."));
    }

    public async Task<(HttpStatusCode, IDataResult<string>)> ResetWithTemporaryPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var admin = await _adminUserDal.GetTrackedAsync(a => a.Email == email, cancellationToken);
        if (admin is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<string>("Bu e-postayla yönetici yok."));
        }

        var temporary = RandomNumberGenerator.GetString("abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ", 10)
                        + "-" + RandomNumberGenerator.GetString("23456789", 4);
        SetPassword(admin, temporary);
        admin.MustChangePassword = true;
        // Sunucuya erişimi olan kurtarır: kayıp doğrulayıcı cihaz da bu yolla aşılır.
        DisableTotp(admin);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<string>(temporary, "Geçici parola verildi; ilk girişte değiştirilmeli."));
    }

    public async Task<(HttpStatusCode, IDataResult<AdminUser>)> BeginTotpSetupAsync(int adminId, CancellationToken cancellationToken = default)
    {
        var admin = await _adminUserDal.GetTrackedAsync(a => a.Id == adminId, cancellationToken);
        if (admin is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<AdminUser>("Yönetici bulunamadı."));
        }

        // Yarım kalan kurulumun anahtarı korunur: sayfa yenilense de uygulamaya okutulmuş kod geçerli kalır.
        if (!admin.TotpEnabled && admin.TotpSecret is null)
        {
            admin.TotpSecret = Totp.NewSecret();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (HttpStatusCode.OK, new SuccessDataResult<AdminUser>(admin));
    }

    public async Task<(HttpStatusCode, IDataResult<IReadOnlyList<string>>)> EnableTotpAsync(int adminId, string code, CancellationToken cancellationToken = default)
    {
        var admin = await _adminUserDal.GetTrackedAsync(a => a.Id == adminId, cancellationToken);
        if (admin is not { TotpEnabled: false, TotpSecret: { } secret })
        {
            return (HttpStatusCode.Conflict, new ErrorDataResult<IReadOnlyList<string>>("İki adımlı doğrulama zaten açık ya da kurulum başlamadı."));
        }

        if (!Totp.Verify(secret, code.Trim(), _clock.GetUtcNow()))
        {
            return (HttpStatusCode.BadRequest, new ErrorDataResult<IReadOnlyList<string>>(CodeMessage));
        }

        var codes = Enumerable.Range(0, RecoveryCodeCount)
            .Select(_ => RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 10).Insert(5, "-"))
            .ToList();
        admin.TotpEnabled = true;
        admin.RecoveryCodeHashes = string.Join(';', codes.Select(c => Sha256(c.Replace("-", string.Empty))));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<IReadOnlyList<string>>(codes, "İki adımlı doğrulama açıldı."));
    }

    public async Task<(HttpStatusCode, IResult)> DisableTotpAsync(int adminId, string password, CancellationToken cancellationToken = default)
    {
        var admin = await _adminUserDal.GetTrackedAsync(a => a.Id == adminId, cancellationToken);
        if (admin is null)
        {
            return (HttpStatusCode.NotFound, new ErrorResult("Yönetici bulunamadı."));
        }

        if (_passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            return (HttpStatusCode.BadRequest, new ErrorResult("Parola hatalı; iki adımlı doğrulama açık kaldı."));
        }

        DisableTotp(admin);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessResult("İki adımlı doğrulama kapatıldı."));
    }

    public async Task<(HttpStatusCode, IDataResult<AdminUser>)> RevokeSessionsAsync(int adminId, CancellationToken cancellationToken = default)
    {
        var admin = await _adminUserDal.GetTrackedAsync(a => a.Id == adminId, cancellationToken);
        if (admin is null)
        {
            return (HttpStatusCode.NotFound, new ErrorDataResult<AdminUser>("Yönetici bulunamadı."));
        }

        admin.PasswordChangedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (HttpStatusCode.OK, new SuccessDataResult<AdminUser>(admin, "Diğer tüm oturumlar kapatıldı."));
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

    /// <summary>Damga ilerleyince diğer tarayıcılardaki çerezler bir sonraki istekte düşer; açık sıfırlama bağlantısı da geçersizleşir.</summary>
    private void SetPassword(AdminUser admin, string password)
    {
        admin.PasswordHash = _passwordHasher.HashPassword(admin, password);
        admin.PasswordChangedAt = DateTime.UtcNow;
        admin.FailedAttempts = 0;
        admin.LockedUntil = null;
        admin.MustChangePassword = false;
        admin.ResetTokenHash = null;
        admin.ResetTokenExpiresAt = null;
    }

    private async Task FailAsync(AdminUser admin, CancellationToken cancellationToken)
    {
        admin.FailedAttempts++;
        if (admin.FailedAttempts >= MaxFailedAttempts)
        {
            admin.LockedUntil = DateTime.UtcNow.Add(LockDuration);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Kabul edilen adım saklanır: omuz üstünden ya da ağdan görülen kod, pencere kapanmadan ikinci kez kullanılamaz.</summary>
    private static bool UseTotpStep(AdminUser admin, long? step)
    {
        if (step is null || step <= admin.TotpLastStep)
        {
            return false;
        }

        admin.TotpLastStep = step;
        return true;
    }

    /// <summary>Eşleşen yedek kod listeden düşer; ikinci kullanımda bulunmaz.</summary>
    private static bool UseRecoveryCode(AdminUser admin, string code)
    {
        var hashes = (admin.RecoveryCodeHashes ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        var hash = Sha256(code);
        if (code.Length != 10 || !hashes.Remove(hash))
        {
            return false;
        }

        admin.RecoveryCodeHashes = string.Join(';', hashes);
        return true;
    }

    private static void DisableTotp(AdminUser admin)
    {
        admin.TotpEnabled = false;
        admin.TotpSecret = null;
        admin.TotpLastStep = null;
        admin.RecoveryCodeHashes = null;
    }

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
