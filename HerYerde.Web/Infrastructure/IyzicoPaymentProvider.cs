using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HerYerde.Business;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Infrastructure;

/// <summary>İyzico 3D Secure (ödeme + 3DS başlat → dönüşte çekim). SDK yerine doğrudan HTTP: IYZWSv2 imzası.
/// İstek gövdesi (kart bilgisi) hiçbir yere yazılmaz; hata olursa yalnız sağlayıcının mesajı döner.</summary>
public sealed partial class IyzicoPaymentProvider(HttpClient http, IOptions<IyzicoSettings> options) : IPaymentProvider
{
    private const string InitPath = "/payment/3dsecure/initialize";
    private const string AuthPath = "/payment/3dsecure/auth";
    private const string CancelPath = "/payment/cancel";
    private const string RefundPath = "/v2/payment/refund";
    private const string InstallmentPath = "/payment/iyzipos/installment";

    private IyzicoSettings Settings => options.Value;

    public string Name => "iyzico";

    public async Task<PaymentInitResult> InitThreeDsAsync(PaymentInitRequest request, CancellationToken cancellationToken = default)
    {
        var order = request.Order;
        var names = order.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var address = new JsonObject
        {
            ["contactName"] = order.FullName,
            ["city"] = order.City,
            ["country"] = "Turkey",
            ["address"] = $"{order.Address} {order.District}/{order.City}"
        };

        var lines = request.Items
            .Where(i => i.UnitPrice * i.Quantity > 0m)
            .Select(i => (Id: i.Id.ToString(CultureInfo.InvariantCulture), i.ProductName, Price: i.UnitPrice * i.Quantity))
            .ToList();

        if (order.ShippingFee > 0m)
        {
            lines.Add(("kargo", "Kargo", order.ShippingFee));
        }

        var basket = new JsonArray();
        foreach (var (id, name, price) in Discounted(lines, order.Discount))
        {
            basket.Add(BasketItem(id, name, price));
        }

        var body = new JsonObject
        {
            ["locale"] = "tr",
            ["conversationId"] = request.ConversationId,
            ["price"] = Price(order.Total),
            ["paidPrice"] = Price(order.Total),
            ["currency"] = "TRY",
            ["installment"] = 1,
            ["basketId"] = order.OrderNo,
            ["paymentChannel"] = "WEB",
            ["paymentGroup"] = "PRODUCT",
            ["callbackUrl"] = request.CallbackUrl,
            ["paymentCard"] = new JsonObject
            {
                ["cardHolderName"] = request.Card.HolderName,
                ["cardNumber"] = request.Card.Number,
                ["expireMonth"] = request.Card.ExpireMonth,
                ["expireYear"] = request.Card.ExpireYear,
                ["cvc"] = request.Card.Cvc,
                ["registerCard"] = 0
            },
            ["buyer"] = new JsonObject
            {
                ["id"] = order.OrderNo,
                ["name"] = names.FirstOrDefault() ?? order.FullName,
                ["surname"] = names.Length > 1 ? names[1] : names.FirstOrDefault() ?? order.FullName,
                ["gsmNumber"] = "+9" + order.Phone,
                ["email"] = order.Email,
                // TCKN toplanmıyor; İyzico bu durumda 11 haneli yer tutucuyu kabul eder.
                ["identityNumber"] = "11111111111",
                ["registrationAddress"] = $"{order.Address} {order.District}/{order.City}",
                ["ip"] = request.BuyerIp,
                ["city"] = order.City,
                ["country"] = "Turkey"
            },
            ["shippingAddress"] = address.DeepClone(),
            ["billingAddress"] = address,
            ["basketItems"] = basket
        };

        var (json, raw) = await PostAsync(InitPath, body, cancellationToken);
        if (json is null || Text(json, "status") != "success")
        {
            return new PaymentInitResult(false, null, null, ErrorMessage(json), raw);
        }

        var paymentId = Text(json, "paymentId");
        // Sandbox her başarılı yanıtı imzalar; imzasız "success" kabul edilmez.
        if (Text(json, "signature") is not { } signature || !Matches(signature, paymentId, Text(json, "conversationId")))
        {
            return new PaymentInitResult(false, paymentId, null, "Sağlayıcı yanıtının imzası doğrulanamadı.", raw);
        }

        var html = Text(json, "threeDSHtmlContent") is { } encoded
            ? Encoding.UTF8.GetString(Convert.FromBase64String(encoded))
            : string.Empty;
        var form = ParseForm(html);
        return form is null
            ? new PaymentInitResult(false, paymentId, null, "3D doğrulama formu okunamadı.", raw)
            // 3DS sayfası büyük ve kişisel veri taşıyabilir; saklanan yanıta girmez.
            : new PaymentInitResult(true, paymentId, form, null, WithoutHtml(json));
    }

    public bool IsValidCallback(PaymentCallback callback)
        => callback.Signature is { Length: > 0 } signature
           && Matches(signature, callback.ConversationData, callback.ConversationId, callback.MdStatus, callback.PaymentId, callback.Status);

    public async Task<PaymentAuthResult> CompleteThreeDsAsync(PaymentCallback callback, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["locale"] = "tr",
            ["conversationId"] = callback.ConversationId,
            ["paymentId"] = callback.PaymentId,
            ["conversationData"] = callback.ConversationData
        };

        var (json, raw) = await PostAsync(AuthPath, body, cancellationToken);
        if (json is null || Text(json, "status") != "success")
        {
            return new PaymentAuthResult(false, callback.PaymentId, 0m, ErrorMessage(json), raw);
        }

        var paidPrice = decimal.TryParse(Text(json, "paidPrice"), NumberStyles.Number, CultureInfo.InvariantCulture, out var paid) ? paid : -1m;
        var signed = Text(json, "signature") is { } signature && Matches(
            signature,
            Text(json, "paymentId"),
            Text(json, "currency"),
            Text(json, "basketId"),
            Text(json, "conversationId"),
            Trimmed(Text(json, "paidPrice")),
            Trimmed(Text(json, "price")));

        return signed
            ? new PaymentAuthResult(true, Text(json, "paymentId"), paidPrice, null, raw)
            : new PaymentAuthResult(false, Text(json, "paymentId"), 0m, "Sağlayıcı yanıtının imzası doğrulanamadı.", raw);
    }

    /// <summary>Tam tutarda önce iptal (gün sonu mutabakatından önce, karta hiç yansımaz); iptal reddedilirse ya da iade kısmiyse
    /// tutar kadar iade (v2, paymentId ile). İptal ödemenin tamamını geri verdiği için kısmi iadede hiç denenmez.</summary>
    public async Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Partial)
        {
            var (cancel, cancelRaw) = await PostAsync(CancelPath, new JsonObject
            {
                ["locale"] = "tr",
                ["conversationId"] = request.ConversationId,
                ["paymentId"] = request.PaymentId,
                ["ip"] = request.Ip
            }, cancellationToken);
            if (cancel is not null && Text(cancel, "status") == "success")
            {
                return new PaymentRefundResult(true, null, cancelRaw);
            }
        }

        var (refund, refundRaw) = await PostAsync(RefundPath, new JsonObject
        {
            ["locale"] = "tr",
            ["conversationId"] = request.ConversationId,
            ["paymentId"] = request.PaymentId,
            ["price"] = Price(request.Amount),
            ["currency"] = "TRY",
            ["ip"] = request.Ip
        }, cancellationToken);
        return refund is not null && Text(refund, "status") == "success"
            ? new PaymentRefundResult(true, null, refundRaw)
            : new PaymentRefundResult(false, ErrorMessage(refund), refundRaw);
    }

    /// <summary>Taksit tablosu. Kart bilgisi taşımaz: yalnız tutar ve isteğe bağlı BIN gider, yanıt saklanmaz.</summary>
    public async Task<InstallmentResult> GetInstallmentsAsync(decimal price, string? bin, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["locale"] = "tr",
            ["conversationId"] = Guid.NewGuid().ToString("N"),
            ["price"] = Price(price)
        };

        if (bin is { Length: 6 })
        {
            body["binNumber"] = bin;
        }

        var (json, _) = await PostAsync(InstallmentPath, body, cancellationToken);
        if (json is null || Text(json, "status") != "success")
        {
            return new InstallmentResult(false, null, ErrorMessage(json));
        }

        // BIN verilmediğinde sağlayıcı her banka için ayrı satır döner; genel tabloda ilki yeter.
        if ((json["installmentDetails"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault() is not { } detail)
        {
            return new InstallmentResult(false, null, "Taksit bilgisi bulunamadı.");
        }

        var options = new List<InstallmentOption>();
        foreach (var row in (detail["installmentPrices"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Number(row, "installmentNumber") is { } count
                && Number(row, "installmentPrice") is { } monthly
                && Number(row, "totalPrice") is { } total)
            {
                options.Add(new InstallmentOption((int)count, monthly, total));
            }
        }

        return options.Count == 0
            ? new InstallmentResult(false, null, "Taksit bilgisi bulunamadı.")
            : new InstallmentResult(
                true,
                new InstallmentTable(
                    options.OrderBy(o => o.Count).ToList(),
                    Text(detail, "bankName"),
                    Text(detail, "cardFamilyName")),
                null);
    }

    /// <summary>İyzico sayıları kimi alanda dizge kimi alanda sayı yollar; ikisi de aynı yoldan okunur.</summary>
    private static decimal? Number(JsonObject json, string key)
        => decimal.TryParse(Text(json, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>İyzico'nun döndürdüğü otomatik gönderilen form: action ve gizli alanlar. Satır içi script CSP'ye
    /// takılacağı için form bizim sayfamızda yeniden kurulur, gönderimi site.js yapar.</summary>
    public static ThreeDsForm? ParseForm(string html)
    {
        var form = FormPattern().Match(html);
        if (!form.Success || !Uri.TryCreate(WebUtility.HtmlDecode(form.Groups["action"].Value), UriKind.Absolute, out var action)
            || action.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var fields = new Dictionary<string, string>();
        foreach (Match input in InputPattern().Matches(html))
        {
            var attributes = AttributePattern().Matches(input.Value)
                .ToDictionary(a => a.Groups["name"].Value.ToLowerInvariant(), a => WebUtility.HtmlDecode(a.Groups["value"].Value));
            if (attributes.TryGetValue("name", out var name) && name.Length > 0)
            {
                fields[name] = attributes.GetValueOrDefault("value") ?? string.Empty;
            }
        }

        return new ThreeDsForm(action.ToString(), fields);
    }

    private async Task<(JsonObject? Json, string Raw)> PostAsync(string path, JsonObject body, CancellationToken cancellationToken)
    {
        var payload = body.ToJsonString();
        var randomKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
                        + RandomNumberGenerator.GetHexString(8, lowercase: true);
        var signature = Hex(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Settings.SecretKey),
            Encoding.UTF8.GetBytes(randomKey + path + payload)));
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"apiKey:{Settings.ApiKey}&randomKey:{randomKey}&signature:{signature}"));

        using var message = new HttpRequestMessage(HttpMethod.Post, Settings.BaseUrl.TrimEnd('/') + path)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        message.Headers.TryAddWithoutValidation("Authorization", "IYZWSv2 " + authorization);
        message.Headers.TryAddWithoutValidation("x-iyzi-rnd", randomKey);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            return (JsonNode.Parse(raw) as JsonObject, raw);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (null, "{\"status\":\"failure\",\"errorMessage\":\"" + exception.GetType().Name + "\"}");
        }
    }

    /// <summary>Yanıt imzası: alanlar ":" ile birleşir, gizli anahtarla HMAC-SHA256, küçük harf hex.</summary>
    private bool Matches(string signature, params string?[] fields)
    {
        var expected = Hex(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Settings.SecretKey),
            Encoding.UTF8.GetBytes(string.Join(':', fields.Select(f => f ?? string.Empty)))));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature.ToLowerInvariant()));
    }

    /// <summary>İyzico sepet satırlarının toplamının price'a eşit olmasını ister; eksi satır kabul etmez. Kupon indirimi
    /// satırlara tutarları oranında dağıtılır, yuvarlama artığı en büyük satıra yazılır (orada yer vardır).</summary>
    private static List<(string Id, string ProductName, decimal Price)> Discounted(
        List<(string Id, string ProductName, decimal Price)> lines,
        decimal discount)
    {
        var total = lines.Sum(l => l.Price);
        if (discount <= 0m || total <= 0m || lines.Count == 0)
        {
            return lines;
        }

        var biggest = lines.IndexOf(lines.MaxBy(l => l.Price));
        var adjusted = new List<(string Id, string ProductName, decimal Price)>(lines.Count);
        var shared = 0m;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var share = index == biggest ? 0m : Math.Round(discount * line.Price / total, 2, MidpointRounding.AwayFromZero);
            shared += share;
            adjusted.Add((line.Id, line.ProductName, line.Price - share));
        }

        var rest = adjusted[biggest];
        adjusted[biggest] = (rest.Id, rest.ProductName, rest.Price - (discount - shared));
        return adjusted;
    }

    private static JsonObject BasketItem(string id, string name, decimal price) => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["category1"] = "Genel",
        ["itemType"] = "PHYSICAL",
        ["price"] = Price(price)
    };

    private static string Price(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>İmzada tutarlar sondaki sıfırları atılmış yazılır ("10.50" → "10.5", "10.00" → "10").</summary>
    private static string? Trimmed(string? value)
        => value is null || !value.Contains('.') ? value : value.TrimEnd('0').TrimEnd('.');

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static string? Text(JsonObject json, string key)
        => json[key] is JsonValue value ? value.ToString() : null;

    private static string ErrorMessage(JsonObject? json)
        => json is not null && Text(json, "errorMessage") is { Length: > 0 } message ? message : "Sağlayıcıya ulaşılamadı.";

    private static string WithoutHtml(JsonObject json)
    {
        json.Remove("threeDSHtmlContent");
        return json.ToJsonString();
    }

    [GeneratedRegex("<form\\b[^>]*\\baction\\s*=\\s*[\"'](?<action>[^\"']+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex FormPattern();

    [GeneratedRegex("<input\\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputPattern();

    [GeneratedRegex("(?<name>[a-zA-Z-]+)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)')")]
    private static partial Regex AttributePattern();
}
