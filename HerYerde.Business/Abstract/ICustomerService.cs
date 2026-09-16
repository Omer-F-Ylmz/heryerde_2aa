using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.DataAccess.Abstract;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

/// <summary>İsteğe bağlı üye hesabı. Hesabın varlığını ele veren yanıt yok: kayıt, giriş bağlantısı ve parola sıfırlama
/// kayıtlı olsun olmasın aynı iletiyi döner; posta yalnız kayıtlı adrese gider.</summary>
public interface ICustomerService
{
    /// <summary>Yeni adreste doğrulanmamış hesap açıp doğrulama bağlantısı; doğrulanmamış hesapta yeni doğrulama bağlantısı
    /// (parola değişmez); doğrulanmış hesapta giriş bağlantısı. Kaydeder.</summary>
    Task<(HttpStatusCode, IResult)> SignUpAsync(CustomerSignUp draft, CancellationToken cancellationToken = default);

    /// <summary>Doğrulama bağlantısının hesabı; bağlantı geçersizse null. Durumu değiştirmez.</summary>
    Task<Customer?> PeekVerifyAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Parolalı hesapta parola da istenir (kayıttaki parolanın bağlantıyı açanın olduğu kanıtlanır). Doğrular,
    /// aynı e-postalı misafir siparişlerini ve çeyiz listelerini bağlar. Kaydeder.</summary>
    Task<(HttpStatusCode, IDataResult<Customer>)> VerifyEmailAsync(string token, string? password, CancellationToken cancellationToken = default);

    /// <summary>Yalnız e-postası doğrulanmış, parolalı hesap. Hata iletisi her durumda aynı.</summary>
    Task<(HttpStatusCode, IDataResult<Customer>)> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>15 dakikalık tek kullanımlık giriş bağlantısı. Kaydeder.</summary>
    Task<(HttpStatusCode, IResult)> RequestLoginLinkAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Bağlantıyı tüketip hesabı döner; doğrulanmamış hesabı doğrular ve kanıtlanmamış kayıt parolasını siler. Kaydeder.</summary>
    Task<(HttpStatusCode, IDataResult<Customer>)> SignInWithLinkAsync(string token, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Yeni parola; oturum damgası ilerler (diğer oturumlar düşer), e-posta doğrulanmış sayılır. Kaydeder.</summary>
    Task<(HttpStatusCode, IResult)> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<Customer>)> ChangePasswordAsync(int customerId, string? currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Oturum damgasını ilerletir: bu tarayıcı yeniden imzalanır, diğerleri ilk istekte düşer.</summary>
    Task<(HttpStatusCode, IDataResult<Customer>)> RevokeSessionsAsync(int customerId, CancellationToken cancellationToken = default);

    Task<bool> StampIsCurrentAsync(int customerId, long stamp, CancellationToken cancellationToken = default);

    Task<Customer?> GetAsync(int customerId, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<Customer>)> UpdateProfileAsync(int customerId, CustomerProfile profile, CancellationToken cancellationToken = default);

    Task<List<CustomerAddress>> GetAddressesAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Id 0 ise ekler; varsayılan işaretlenirse ya da ilk adresse diğerlerinin varsayılanlığı kalkar.</summary>
    Task<(HttpStatusCode, IResult)> SaveAddressAsync(int customerId, CustomerAddress address, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IResult)> DeleteAddressAsync(int customerId, int addressId, CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetFavoriteIdsAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Favorideyse çıkarır, değilse ekler; sonuç favoride olup olmadığı.</summary>
    Task<(HttpStatusCode, IDataResult<bool>)> ToggleFavoriteAsync(int customerId, int productId, CancellationToken cancellationToken = default);

    /// <summary>Yayındaki favori ürünler, en son eklenen önce.</summary>
    Task<List<Product>> GetFavoriteProductsAsync(int customerId, CancellationToken cancellationToken = default);

    Task<List<Order>> GetOrdersAsync(int customerId, CancellationToken cancellationToken = default);

    Task<List<ReviewRow>> GetReviewsAsync(int customerId, CancellationToken cancellationToken = default);

    Task<List<GiftRegistry>> GetRegistriesAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Açık sipariş varsa 409. Yoksa üyenin siparişleri anonimleştirilir, yorum adları maskelenir, e-postasıyla gelen
    /// iletişim mesajları ve kendi çeyiz listeleri silinir, hesap adres ve favorileriyle silinir. Silinecek gizli dosyalar
    /// sonuçta döner (çağıranın işi). Kaydeder.</summary>
    Task<(HttpStatusCode, IDataResult<CustomerDeletion>)> DeleteAccountAsync(int customerId, CancellationToken cancellationToken = default);
}
