using System.Net;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

public interface IAdminAuthService
{
    Task<(HttpStatusCode, IDataResult<AdminUser>)> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>İlk yöneticiyi oluşturur; e-posta zaten varsa dokunmaz.</summary>
    Task<(HttpStatusCode, IResult)> EnsureSeedAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Mevcut parola doğruysa yenisini yazar ve damgayı ilerletir; yeni damgayla döner.</summary>
    Task<(HttpStatusCode, IDataResult<AdminUser>)> ChangePasswordAsync(
        int adminId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Çerezdeki parola damgası hâlâ geçerli mi; değilse oturum düşer.</summary>
    Task<bool> StampIsCurrentAsync(int adminId, long stamp, CancellationToken cancellationToken = default);
}
