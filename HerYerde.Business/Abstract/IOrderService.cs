using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Core.Utilities.Results;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Abstract;

public interface IOrderService
{
    /// <summary>Sepeti siparişe çevirir: stok düşer, sepet boşalır. Stok yetmezse 409 ile hiçbiri olmaz.
    /// Kartla ödemede stok ve sepet ödeme onayına (IPaymentService.CompleteAsync) kalır; Payment kaydı açılır.</summary>
    Task<(HttpStatusCode, IDataResult<Order>)> PlaceAsync(Guid cartId, OrderDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Yönetimden sipariş: kalemler stok kodu/slug ile, stok aynı işlemde düşer (yetmezse 409, hiçbiri olmaz).
    /// Kart seçilemez; kaynak kaydedilir; müşteri postası istenirse kuyruğa girer.</summary>
    Task<(HttpStatusCode, IDataResult<Order>)> PlaceManualAsync(ManualOrderDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Beklemede/Onaylandı siparişte teslimat bilgisi ve adetler değişir, stok farkı düşer ya da geri döner;
    /// sonraki durumlarda 409. Veri, denetim izi için "alan: eski → yeni" özetidir.</summary>
    Task<(HttpStatusCode, IDataResult<string>)> EditAsync(int orderId, OrderEdit edit, CancellationToken cancellationToken = default);

    /// <summary>Fatura numarası, tarihi ve gizli depodaki PDF yolunu yazar; veri, yerini alan eski dosyanın yolu.</summary>
    Task<(HttpStatusCode, IDataResult<string?>)> SetInvoiceAsync(
        int orderId,
        string invoiceNo,
        DateTime invoiceDate,
        string file,
        CancellationToken cancellationToken = default);

    /// <summary>Müşterinin havale bildirimi; yalnız sipariş anahtarıyla ve bekleyen havale siparişinde.</summary>
    Task<(HttpStatusCode, IResult)> SubmitPaymentNoticeAsync(
        string orderNo,
        Guid accessToken,
        PaymentNoticeDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>Havale hesaba geçti: bildirimler onaylanır, sipariş Onaylandı olur, müşteriye posta kuyruğa girer.</summary>
    Task<(HttpStatusCode, IResult)> ApprovePaymentAsync(int orderId, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<OrderDetail>)> GetByOrderNoAsync(string orderNo, CancellationToken cancellationToken = default);

    Task<(HttpStatusCode, IDataResult<OrderDetail>)> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Müşterinin sipariş sorgulaması: numara ve tam telefon (tek biçime indirgenmiş) birlikte eşleşmeli.
    /// Hangi alanın tutmadığı söylenmez; anonimleştirilmiş sipariş hiç bulunmaz.</summary>
    Task<(HttpStatusCode, IDataResult<Order>)> LookupAsync(string orderNo, string phone, CancellationToken cancellationToken = default);

    /// <summary>Yönetim listesi: duruma göre süzer, sipariş numarası veya telefonla arar.
    /// uninvoicedDelivered ise yalnız faturası girilmemiş teslim edilmiş siparişler (durum süzgeci yok sayılır).</summary>
    Task<(HttpStatusCode, IDataResult<List<Order>>)> SearchAsync(
        OrderStatus? status,
        string? query,
        bool uninvoicedDelivered = false,
        CancellationToken cancellationToken = default);

    /// <summary>Yalnız <see cref="Rules.OrderRules.CanTransition"/> izin verirse; aksi halde 400.
    /// <see cref="OrderStatus.Kargoda"/> geçişinde tanımlı kargo firması ve takip numarası zorunlu.</summary>
    Task<(HttpStatusCode, IResult)> ChangeStatusAsync(
        int orderId,
        OrderStatus next,
        string? carrier = null,
        string? trackingNo = null,
        CancellationToken cancellationToken = default);

    /// <summary>CSV dökümü: duruma ve [fromUtc, toUtc) aralığına göre siparişler kalemleriyle (hediye dahil), eskiden yeniye; kalemler tek sorguda.</summary>
    Task<List<OrderDetail>> ExportAsync(OrderStatus? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);

    /// <summary>Yönetimde henüz açılmamış sipariş sayısı; başlıktaki rozet buradan.</summary>
    Task<(HttpStatusCode, IDataResult<int>)> UnseenCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Siparişi görüldü sayar; ilk görülme anı korunur.</summary>
    Task<(HttpStatusCode, IResult)> MarkSeenAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Kapanmış (teslim/iptal) siparişin ad, telefon, e-posta ve adresini maskeler, notu siler, erişim anahtarını yeniler;
    /// açık siparişte 409. İade talebinin neden, IBAN ve fotoğrafı da silinir. Data: kayıttan düşen dekont ve iade fotoğrafı dosyaları
    /// (silinmeleri çağıranın işi).</summary>
    Task<(HttpStatusCode, IDataResult<IReadOnlyList<string>>)> AnonymizeAsync(int orderId, CancellationToken cancellationToken = default);
}
