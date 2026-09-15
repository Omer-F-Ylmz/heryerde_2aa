using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;

namespace HerYerde.Business.Abstract;

/// <summary>Cayma/iade ve değişim talepleri ile müşterinin sipariş iptali.</summary>
public interface IReturnService
{
    /// <summary>Müşteri talebi; yalnız sipariş anahtarıyla, teslim edilmiş siparişte ve teslimden itibaren 14 gün içinde (dışında 400).
    /// Kalem adedi iade edilebilir adedi aşamaz; değişimde aynı ürünün stokta olan başka varyantı seçilir (stok yoksa 409);
    /// kartla ödenmemiş siparişte iade için IBAN zorunlu.</summary>
    Task<(HttpStatusCode, IDataResult<ReturnRequest>)> RequestAsync(
        string orderNo,
        Guid accessToken,
        ReturnDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>Talep formu için sipariş kalemleri; talep açılamıyorsa RequestAsync'in durum kodu ve mesajı.</summary>
    Task<(HttpStatusCode, IDataResult<ReturnForm>)> GetFormAsync(string orderNo, Guid accessToken, CancellationToken cancellationToken = default);

    /// <summary>Bekleyen talep onaylanır; müşteriye iade adresi ve kargo bilgisi postası kuyruğa girer.</summary>
    Task<(HttpStatusCode, IResult)> ApproveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Bekleyen talep gerekçeyle reddedilir (gerekçe zorunlu); müşteriye gerekçeli posta.</summary>
    Task<(HttpStatusCode, IResult)> RejectAsync(int id, string reason, CancellationToken cancellationToken = default);

    /// <summary>Onaylı talebin ürünü geldi: kalem stokları iade edilir. Kartlı iadede sağlayıcıdan kalem tutarı (tüm kalemler dönerse
    /// kargo dahil) geri ödenir ve talep tamamlanır (sağlayıcı reddederse 502, hiçbir şey değişmez); değişimde yeni varyantın stoğu düşer
    /// (yetmezse 409). İkinci teslim alma 409.</summary>
    Task<(HttpStatusCode, IResult)> ReceiveAsync(int id, string ip, CancellationToken cancellationToken = default);

    /// <summary>Havale/kapıda ödemeli siparişin teslim alınmış iadesi IBAN'a elle ödendi.</summary>
    Task<(HttpStatusCode, IResult)> MarkRefundedAsync(int id, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<List<ReturnListItem>>)> GetAllAsync(CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<ReturnDetail>)> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Siparişin talepleri, eskiden yeniye (müşteri sipariş sayfası).</summary>
    Task<List<ReturnDetail>> GetForOrderAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Müşteri iptali: yalnız Beklemede/Onaylandı (sonrası 409). Kartla ödenmişse ödeme iade edilir; onaylı havalede tutar ve IBAN
    /// elle geri ödeme için işaretlenir (IBAN zorunlu); stok geri döner.</summary>
    Task<(HttpStatusCode, IResult)> CancelByCustomerAsync(
        string orderNo,
        Guid accessToken,
        string? iban,
        string ip,
        CancellationToken cancellationToken = default);

    /// <summary>Müşteri iptalindeki onaylı havalenin elle geri ödemesi yapıldı.</summary>
    Task<(HttpStatusCode, IResult)> MarkOrderRefundedAsync(int orderId, CancellationToken cancellationToken = default);
}
