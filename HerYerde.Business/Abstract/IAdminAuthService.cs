using System.Net;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IAdminAuthService
{
    /// <summary>Parola doğrulaması. Başarıda iki adımlı doğrulama açıksa çağıran kodu ayrıca istemeli
    /// (<see cref="VerifySecondFactorAsync"/>); <see cref="AdminUser.MustChangePassword"/> ise parola değiştirilmeli.</summary>
    Task<(HttpStatusCode, IDataResult<AdminUser>)> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>TOTP kodu ya da tek kullanımlık yedek kod. Hatalar parola hatalarıyla aynı sayaçta; 5'te hesap 15 dk kilitlenir.</summary>
    Task<(HttpStatusCode, IDataResult<AdminUser>)> VerifySecondFactorAsync(int adminId, string code, CancellationToken cancellationToken = default);

    /// <summary>İlk yöneticiyi oluşturur; e-posta zaten varsa dokunmaz.</summary>
    Task<(HttpStatusCode, IResult)> EnsureSeedAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Mevcut parola doğruysa yenisini yazar ve damgayı ilerletir; yeni damgayla döner.</summary>
    Task<(HttpStatusCode, IDataResult<AdminUser>)> ChangePasswordAsync(
        int adminId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Kayıtlı e-postaya 30 dakikalık, tek kullanımlık bağlantı kuyruğa girer; bilinmeyen e-postada da aynı yanıt.</summary>
    Task<(HttpStatusCode, IResult)> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Bağlantı anahtarı geçerliyse parolayı yazar ve anahtarı siler; süresi dolmuş/kullanılmış anahtarda 400.</summary>
    Task<(HttpStatusCode, IResult)> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>CLI kurtarma: geçici parola üretir (veri), ilk girişte değiştirme zorunlu olur, iki adımlı doğrulama kapanır.</summary>
    Task<(HttpStatusCode, IDataResult<string>)> ResetWithTemporaryPasswordAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Kapalıysa yeni gizli anahtar üretir; açıksa olduğu gibi döner.</summary>
    Task<(HttpStatusCode, IDataResult<AdminUser>)> BeginTotpSetupAsync(int adminId, CancellationToken cancellationToken = default);

    /// <summary>Kurulumdaki anahtarla üretilmiş kod doğruysa açar; veri, bir kez gösterilecek 8 yedek kod.</summary>
    Task<(HttpStatusCode, IDataResult<IReadOnlyList<string>>)> EnableTotpAsync(int adminId, string code, CancellationToken cancellationToken = default);

    /// <summary>Parola doğruysa kapatır, anahtar ve yedek kodlar silinir.</summary>
    Task<(HttpStatusCode, IResult)> DisableTotpAsync(int adminId, string password, CancellationToken cancellationToken = default);

    /// <summary>Damgayı ilerletir: diğer tüm tarayıcılardaki oturumlar düşer; çağıran kendi çerezini yeni damgayla yeniler.</summary>
    Task<(HttpStatusCode, IDataResult<AdminUser>)> RevokeSessionsAsync(int adminId, CancellationToken cancellationToken = default);

    /// <summary>Çerezdeki parola damgası hâlâ geçerli mi; değilse oturum düşer.</summary>
    Task<bool> StampIsCurrentAsync(int adminId, long stamp, CancellationToken cancellationToken = default);
}
