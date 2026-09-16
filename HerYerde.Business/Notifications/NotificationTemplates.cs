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

    public static (string Subject, string Body) PaymentApproved(Order order, string thankYouUrl)
    {
        var body = new StringBuilder();
        body.Append(Heading("Ödemeniz onaylandı", $"{Encode(order.OrderNo)} numaralı siparişinizin havalesi hesabımıza geçti."));
        body.Append(Row("Tutar", Tl(order.Total)));
        body.Append(Paragraph("Siparişiniz hazırlanmak üzere sıraya alındı; kargoya verilince ayrıca haber vereceğiz."));
        body.Append(Button(thankYouUrl, "Siparişimi görüntüle"));
        return ($"Ödemeniz onaylandı · {order.OrderNo}", Page(body.ToString()));
    }

    public static (string Subject, string Body) ReturnApproved(Order order, ReturnRequest request, string address, string carrier, string orderUrl)
    {
        var kind = request.Type == ReturnType.Degisim ? "Değişim" : "İade";
        var body = new StringBuilder();
        body.Append(Heading($"{kind} talebiniz onaylandı", $"{Encode(order.OrderNo)} numaralı siparişiniz için talebinizi onayladık."));
        body.Append(Row("İade adresi", Encode(address)));
        body.Append(Row("Kargo", Encode(carrier)));
        body.Append(Paragraph("Ürünü faturası ve mümkünse orijinal ambalajıyla, en geç 10 gün içinde bu adrese gönderin. Ürün bize ulaşınca kontrol edip "
            + (request.Type == ReturnType.Degisim ? "yeni ürününüzü kargoya veririz." : "ödemenizi en geç 14 gün içinde iade ederiz.")));
        body.Append(Button(orderUrl, "Siparişimi görüntüle"));
        return ($"{kind} talebiniz onaylandı · {order.OrderNo}", Page(body.ToString()));
    }

    public static (string Subject, string Body) ReturnRejected(Order order, ReturnRequest request, string orderUrl)
    {
        var kind = request.Type == ReturnType.Degisim ? "Değişim" : "İade";
        var body = new StringBuilder();
        body.Append(Heading($"{kind} talebiniz reddedildi", $"{Encode(order.OrderNo)} numaralı siparişiniz için talebinizi kabul edemedik."));
        body.Append(Row("Gerekçe", Encode(request.RejectReason ?? "—")));
        body.Append(Paragraph("Sorunuz varsa bu postayı yanıtlayabilir ya da WhatsApp'tan bize yazabilirsiniz. Uyuşmazlıkta tüketici hakem heyetine başvurma hakkınız saklıdır."));
        body.Append(Button(orderUrl, "Siparişimi görüntüle"));
        return ($"{kind} talebiniz reddedildi · {order.OrderNo}", Page(body.ToString()));
    }

    /// <summary>İşlemsel davet: satın alınan ürünleri değerlendirme bağlantısı. Kampanya içermez (İYS gerektirmez);
    /// bağlantı sipariş numarasını taşır, yorum doğrulanmış alıcı rozetiyle yayınlanır.</summary>
    public static (string Subject, string Body) ReviewInvite(
        Order order,
        IReadOnlyList<(string ProductName, string Url)> products)
    {
        var body = new StringBuilder();
        body.Append(Heading("Ürününüz nasıl?", $"{Encode(order.OrderNo)} numaralı siparişiniz elinize geçeli bir hafta oldu."));
        body.Append(Paragraph("Aldığınız ürünü değerlendirirseniz sizden sonra bakanlara çok yardımı olur. Yorumunuz "
            + "sipariş numaranızla eşleştiği için \"doğrulanmış alıcı\" rozetiyle yayınlanır."));

        foreach (var (name, url) in products)
        {
            body.Append(Row(Encode(name), $"<a href=\"{Encode(url)}\" style=\"color:{Brand}\">Değerlendir</a>"));
        }

        return ($"Ürününüz nasıl? · {order.OrderNo}", Page(body.ToString()));
    }

    /// <summary>Yönetim bağlantısı listenin anahtarıdır: bağlantıyı bilen listeyi değiştirebilir.</summary>
    public static (string Subject, string Body) GiftRegistryCreated(GiftRegistry registry, string manageUrl, string publicUrl)
    {
        var body = new StringBuilder();
        body.Append(Heading("Çeyiz listeniz hazır", $"Merhaba {Encode(registry.OwnerName)}, listenizi açtık."));
        body.Append(Paragraph("Ürün eklemek, adetleri değiştirmek ya da listeyi gizlemek için aşağıdaki yönetim bağlantısını "
            + "kullanın. Bu bağlantı size özeldir, kimseyle paylaşmayın."));
        body.Append(Button(manageUrl, "Listemi yönet"));
        body.Append(Paragraph($"Yakınlarınızla paylaşacağınız adres: <a href=\"{Encode(publicUrl)}\" style=\"color:{Brand}\">{Encode(publicUrl)}</a>"));
        return ("Çeyiz listeniz hazır", Page(body.ToString()));
    }

    /// <summary>Alanın adı sipariş formunda yazdığı addır; ürün satırları yalnız bu listeye ait olanlardır.</summary>
    public static (string Subject, string Body) GiftRegistryPurchase(
        GiftRegistry registry,
        Order order,
        IReadOnlyList<OrderItem> items,
        string manageUrl)
    {
        var body = new StringBuilder();
        body.Append(Heading("Listenize hediye geldi", $"{Encode(order.FullName)} çeyiz listenizden hediye aldı."));
        foreach (var item in items)
        {
            body.Append(Row(Encode(item.ProductName), $"{item.Quantity} adet"));
        }

        body.Append(Paragraph("Listede kalan ürünleri yönetim sayfanızdan görebilirsiniz."));
        body.Append(Button(manageUrl, "Listemi görüntüle"));
        return ($"Çeyiz listenize hediye geldi · {order.OrderNo}", Page(body.ToString()));
    }

    /// <summary>Faturanın indirme bağlantısı siparişin erişim anahtarını taşır; başka siparişin anahtarıyla açılmaz.</summary>
    public static (string Subject, string Body) InvoiceReady(Order order, string invoiceUrl)
    {
        var body = new StringBuilder();
        body.Append(Heading("Faturanız hazır", $"{Encode(order.OrderNo)} numaralı siparişinizin faturasını kestik."));
        body.Append(Row("Fatura no", Encode(order.InvoiceNo ?? "—")));
        body.Append(Row("Fatura tarihi", order.InvoiceDate?.ToString("d MMMM yyyy", Turkish) ?? "—"));
        body.Append(Row("Tutar", Tl(order.Total)));
        body.Append(Paragraph("Faturanızı aşağıdaki bağlantıdan PDF olarak indirebilirsiniz. Bağlantı siparişinize özeldir, paylaşmayın."));
        body.Append(Button(invoiceUrl, "Faturamı indir"));
        return ($"Faturanız hazır · {order.OrderNo}", Page(body.ToString()));
    }

    /// <summary>Bağlantı 30 dakika geçerli ve tek kullanımlık; istenmediyse posta yok sayılabilir.</summary>
    public static (string Subject, string Body) AdminPasswordReset(string resetUrl)
    {
        var body = new StringBuilder();
        body.Append(Heading("Parola sıfırlama", "Yönetim paneli için parola sıfırlama istendi."));
        body.Append(Paragraph("Bağlantı 30 dakika geçerlidir ve bir kez kullanılabilir. Bu isteği siz yapmadıysanız postayı yok sayın; parolanız değişmez."));
        body.Append(Button(resetUrl, "Yeni parola belirle"));
        return ("Yönetim parolası sıfırlama", Page(body.ToString()));
    }

    /// <summary>Müşterinin yazdığı her alan kaçışlanır; mesajdaki satır sonları korunur.</summary>
    public static (string Subject, string Body) ContactMessage(ContactMessage message, string adminUrl)
    {
        var subject = ContactLabels.Subject(message.Subject);
        var body = new StringBuilder();
        body.Append(Heading("İletişim formu", $"{subject} · {Encode(message.Name)}"));
        body.Append(Row("İletişim", Encode(message.Contact)));
        body.Append(Paragraph(Encode(message.Message).ReplaceLineEndings("<br />")));
        body.Append(Button(adminUrl, "Mesajları yönetimde aç"));
        // Ad konu başlığına girer: satır sonu ve denetim karakterleri boşluğa çevrilir.
        var name = string.Concat(message.Name.Select(c => char.IsControl(c) ? ' ' : c)).Replace("  ", " ");
        return ($"İletişim formu · {subject} · {name}", Page(body.ToString()));
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

        // İndirim yazılmazsa ara toplam + kargo, toplamı vermez.
        if (order.Discount > 0m)
        {
            rows.Append(Row($"İndirim ({Encode(order.CouponCode ?? string.Empty)})", "−" + Tl(order.Discount)));
        }

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
