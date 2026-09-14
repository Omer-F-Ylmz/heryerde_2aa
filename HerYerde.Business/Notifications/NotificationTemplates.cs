using System.Globalization;
using System.Net;
using System.Text;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Business.Notifications;

/// <summary>E-posta gövdeleri. Razor değil: arka plan gönderiminde görünüm altyapısı olmasın diye düz HTML,
/// marka renkleri satır içi (posta istemcileri harici CSS yüklemez).</summary>
public static class NotificationTemplates
{
    private const string Ink = "#2B2118";
    private const string Brand = "#A8442A";
    private const string BrandDark = "#5E2214";
    private const string Cream = "#F6F1E8";
    private const string Line = "#E5DBCB";
    private const string Muted = "#6B5B4E";

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static (string Subject, string Body) OrderPlaced(
        Order order,
        IReadOnlyList<OrderItem> items,
        string thankYouUrl,
        string iban)
    {
        var body = new StringBuilder();
        body.Append(Heading("Siparişiniz alındı", $"Teşekkürler {Encode(order.FullName)}, {Encode(order.OrderNo)} numaralı siparişinizi aldık."));
        body.Append(Lines(items, order));
        body.Append(Row("Ödeme", PaymentLabels.Method(order.PaymentMethod)));

        if (order.PaymentMethod == PaymentMethod.HavaleEft && iban.Length > 0)
        {
            body.Append(Paragraph($"Havale açıklamasına sipariş numaranızı yazın:<br /><strong>{Encode(iban)}</strong>"));
        }

        body.Append(Button(thankYouUrl, "Siparişimi görüntüle"));
        return ($"Siparişiniz alındı · {order.OrderNo}", Page(body.ToString()));
    }

    public static (string Subject, string Body) OrderShipped(Order order, string? trackingUrl)
    {
        var body = new StringBuilder();
        body.Append(Heading("Siparişiniz kargoda", $"{Encode(order.OrderNo)} numaralı siparişiniz yola çıktı."));
        body.Append(Row("Kargo firması", Encode(order.Carrier ?? "—")));
        body.Append(Row("Takip numarası", Encode(order.TrackingNo ?? "—")));

        if (trackingUrl is not null)
        {
            body.Append(Button(trackingUrl, "Kargomu takip et"));
        }

        return ($"Siparişiniz kargoda · {order.OrderNo}", Page(body.ToString()));
    }

    public static (string Subject, string Body) NewOrderForStore(
        Order order,
        IReadOnlyList<OrderItem> items,
        string adminUrl)
    {
        var body = new StringBuilder();
        body.Append(Heading("Yeni sipariş", $"{Encode(order.OrderNo)} · {Encode(order.FullName)}"));
        body.Append(Lines(items, order));
        body.Append(Row("Ödeme", PaymentLabels.Method(order.PaymentMethod)));
        body.Append(Row("Teslimat", Encode($"{order.District} / {order.City}")));
        body.Append(Button(adminUrl, "Siparişi yönetimde aç"));
        return ($"Yeni sipariş · {order.OrderNo}", Page(body.ToString()));
    }

    private static string Lines(IReadOnlyList<OrderItem> items, Order order)
    {
        var rows = new StringBuilder();
        foreach (var item in items)
        {
            rows.Append(Row(
                $"{Encode(item.ProductName)} × {item.Quantity}",
                item.IsGift ? "Hediye" : Tl(item.UnitPrice * item.Quantity)));
        }

        rows.Append(Row("Ara toplam", Tl(order.Subtotal)));
        rows.Append(Row("Kargo", order.ShippingFee == 0m ? "Kargo bedava" : Tl(order.ShippingFee)));
        rows.Append(Row("<strong>Toplam</strong>", $"<strong>{Tl(order.Total)}</strong>"));
        return rows.ToString();
    }

    private static string Tl(decimal amount) => amount.ToString("#,##0.00", Turkish) + " ₺";

    /// <summary>Yalnız HTML'i bozan dört karakter kaçırılır; Türkçe harfler UTF-8 gövdede olduğu gibi kalır
    /// (<see cref="WebUtility.HtmlEncode"/> onları sayısal varlığa çevirip okunmaz yapardı).</summary>
    private static string Encode(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    private static string Heading(string eyebrow, string lead) =>
        $"""
         <p style="margin:0 0 4px;font:600 12px/1.4 Arial,sans-serif;letter-spacing:.08em;text-transform:uppercase;color:{Brand}">{eyebrow}</p>
         <p style="margin:0 0 24px;font:600 22px/1.4 Georgia,serif;color:{BrandDark}">{lead}</p>
         """;

    private static string Row(string label, string value) =>
        $"""
         <p style="margin:0;padding:8px 0;border-bottom:1px solid {Line};font:400 15px/1.6 Arial,sans-serif;color:{Ink}">
           <span>{label}</span><span style="float:right">{value}</span>
         </p>
         """;

    private static string Paragraph(string html) =>
        $"""<p style="margin:16px 0;font:400 15px/1.7 Arial,sans-serif;color:{Muted}">{html}</p>""";

    private static string Button(string url, string label) =>
        $"""
         <p style="margin:24px 0 0">
           <a href="{Encode(url)}" style="display:inline-block;padding:12px 24px;border-radius:8px;background:{Brand};color:#FFFFFF;font:600 15px/1 Arial,sans-serif;text-decoration:none">{label}</a>
         </p>
         """;

    private static string Page(string inner) =>
        $"""
         <div style="margin:0;padding:24px;background:{Cream}">
           <div style="max-width:560px;margin:0 auto;padding:32px;background:#FDFAF5;border:1px solid {Line};border-radius:16px">
             <p style="margin:0 0 24px;font:600 20px/1 Georgia,serif;color:{BrandDark}">HerYerde</p>
             {inner}
           </div>
         </div>
         """;
}
