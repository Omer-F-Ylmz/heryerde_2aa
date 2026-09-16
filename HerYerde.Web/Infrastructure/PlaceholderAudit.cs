using HerYerde.Business.Notifications;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Models;

namespace HerYerde.Web.Infrastructure;

/// <summary>YAYIN-KAPI: müşteriye görünen metinlerde kalan taslak işaretleri ("[MÜŞTERİ…]", "[ÖNERİ]").</summary>
public interface IPlaceholderAudit
{
    /// <summary>İşaret kalan kaynakların adları; boşsa temiz. Tarama süreç başına bir kez yapılır.</summary>
    Task<IReadOnlyList<string>> FindingsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Taranan metinler: yasal sayfalar (sözleşme PDF'ine giren iki belge ayrıca adlandırılır), Hakkımızda, İletişim, SSS ve
/// tüm posta şablonları örnek veriyle. Görünümler istek dışında, düzen sayfası olmadan çizilir (RazorViewText); Razor yorumu
/// çıktıya girmediği için dosyadaki taslak notları sayılmaz. Bulgu varsa uyarı loglanır; Production'da ayrıca Sentry'ye uyarı
/// gider ve /health/ready 503 döner (ReadinessCheck).</summary>
public sealed class PlaceholderAudit(
    IServiceScopeFactory scopes,
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ILogger<PlaceholderAudit> logger) : IPlaceholderAudit
{
    public static readonly string[] Markers = ["[MÜŞTERİ", "[ÖNERİ]"];

    private readonly Lock _gate = new();
    private Task<IReadOnlyList<string>>? _scan;

    public Task<IReadOnlyList<string>> FindingsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _scan ??= ScanAsync();
        }

        return _scan.WaitAsync(cancellationToken);
    }

    public static bool HasMarker(string text) => Markers.Any(marker => text.Contains(marker, StringComparison.Ordinal));

    /// <summary>Her posta şablonu bir kez, işaret taşımayan örnek veriyle; yeni şablon eklenince buraya da eklenir (test sayar).</summary>
    public static IEnumerable<(string Name, string Subject, string Body)> MailSamples()
    {
        const string url = "https://ornek.invalid/baglanti";
        var order = new Order { OrderNo = "HY-000000", FullName = "Örnek Müşteri", Phone = "05000000000", Address = "Örnek adres", City = "Ankara", District = "Çankaya", Total = 100m, Subtotal = 100m };
        OrderItem[] items = [new OrderItem { ProductName = "Örnek ürün", Sku = "ORNEK-1", Quantity = 1, UnitPrice = 100m }];
        var request = new ReturnRequest { Reason = "Örnek neden", RejectReason = "Örnek ret nedeni", RefundAmount = 100m };
        var registry = new GiftRegistry { Slug = "ornek", OwnerName = "Örnek Liste", Phone = "05000000000" };

        (string, (string, string))[] samples =
        [
            (nameof(NotificationTemplates.OrderPlaced), NotificationTemplates.OrderPlaced(order, items, url, "TR00 0000 0000 0000 0000 0000 00")),
            (nameof(NotificationTemplates.OrderShipped), NotificationTemplates.OrderShipped(order, url)),
            (nameof(NotificationTemplates.NewOrderForStore), NotificationTemplates.NewOrderForStore(order, items, url)),
            (nameof(NotificationTemplates.PaymentApproved), NotificationTemplates.PaymentApproved(order, url)),
            (nameof(NotificationTemplates.ReturnApproved), NotificationTemplates.ReturnApproved(order, request, "Örnek iade adresi", "Örnek Kargo", url)),
            (nameof(NotificationTemplates.ReturnRejected), NotificationTemplates.ReturnRejected(order, request, url)),
            (nameof(NotificationTemplates.ReviewInvite), NotificationTemplates.ReviewInvite(order, [("Örnek ürün", url)])),
            (nameof(NotificationTemplates.GiftRegistryCreated), NotificationTemplates.GiftRegistryCreated(registry, url, url)),
            (nameof(NotificationTemplates.GiftRegistryPurchase), NotificationTemplates.GiftRegistryPurchase(registry, order, items, url)),
            (nameof(NotificationTemplates.InvoiceReady), NotificationTemplates.InvoiceReady(order, url)),
            (nameof(NotificationTemplates.AdminPasswordReset), NotificationTemplates.AdminPasswordReset(url)),
            (nameof(NotificationTemplates.CustomerVerify), NotificationTemplates.CustomerVerify(url)),
            (nameof(NotificationTemplates.CustomerLoginLink), NotificationTemplates.CustomerLoginLink(url)),
            (nameof(NotificationTemplates.CustomerPasswordReset), NotificationTemplates.CustomerPasswordReset(url)),
            (nameof(NotificationTemplates.ContactMessage), NotificationTemplates.ContactMessage(new ContactMessage { Name = "Örnek", Contact = "05000000000", Message = "Örnek mesaj" }, url))
        ];

        return samples.Select(sample => (sample.Item1, sample.Item2.Item1, sample.Item2.Item2));
    }

    private async Task<IReadOnlyList<string>> ScanAsync()
    {
        // İstek iş parçacığını ve açılışı bekletmez.
        await Task.Yield();
        var findings = new List<string>();
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // Yasal görünümler PdfFlag ile düzen sayfası olmadan çizilir (sözleşme PDF'i de aynı çıktıdan üretilir).
        foreach (var page in LegalPages.All)
        {
            var names = LegalPdfArchive.Slugs.Contains(page.Slug)
                ? new[] { "yasal/" + page.Slug, $"sözleşme PDF'i ({page.Slug})" }
                : ["yasal/" + page.Slug];
            await CheckAsync(findings, names, () => RazorViewText.RenderAsync(services, $"/Views/Legal/{page.Slug}.cshtml", page));
        }

        await CheckAsync(findings, ["hakkimizda"], () => RazorViewText.RenderAsync(services, "/Views/Content/About.cshtml", new AboutPageVm("https://wa.me/")));
        await CheckAsync(findings, ["iletisim"], () => RazorViewText.RenderAsync(
            services, "/Views/Contact/Index.cshtml", new ContactPageVm(new ContactFormViewModel(), false, "https://wa.me/")));
        await CheckAsync(findings, ["sss"], () => Task.FromResult(string.Join('\n', FaqSource.Load()
            .SelectMany(group => group.Items.Select(item => $"{group.Category}\n{item.Question}\n{item.Answer}")))));
        foreach (var (name, subject, body) in MailSamples())
        {
            await CheckAsync(findings, ["posta/" + name], () => Task.FromResult(subject + "\n" + body));
        }

        if (findings.Count > 0 && !lifetime.ApplicationStopping.IsCancellationRequested)
        {
            logger.LogWarning("Müşteriye görünen metinde yer tutucu kaldı ({Count} kaynak): {Sources}", findings.Count, string.Join(", ", findings));
            if (environment.IsProduction() && SentrySdk.IsEnabled)
            {
                SentrySdk.CaptureMessage($"Müşteriye görünen metinde yer tutucu kaldı: {string.Join(", ", findings)}", SentryLevel.Warning);
            }
        }

        return findings;
    }

    private async Task CheckAsync(List<string> findings, string[] names, Func<Task<string>> text)
    {
        if (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            return;
        }

        try
        {
            if (HasMarker(await text()))
            {
                findings.AddRange(names);
            }
        }
        catch (Exception exception) when (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            // Çizilemeyen metin doğrulanamamıştır: yer tutucu gibi sayılır.
            logger.LogError(exception, "Yer tutucu taraması {Source} metnini çizemedi.", names[0]);
            findings.AddRange(names.Select(name => name + " (taranamadı)"));
        }
    }
}
