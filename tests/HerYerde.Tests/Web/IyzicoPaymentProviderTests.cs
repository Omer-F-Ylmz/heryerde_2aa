using HerYerde.Web.Infrastructure;

namespace HerYerde.Tests.Web;

/// <summary>D7: İyzico'nun 3DS HTML'i ağsız ayrıştırılır; satır içi script yerine form bizim sayfamızda kurulur.</summary>
public sealed class IyzicoPaymentProviderTests
{
    [Fact]
    public void Uc_boyutlu_form_action_ve_gizli_alanlariyla_okunur()
    {
        const string html = "<!doctype html><html><body><form id=\"iyzico-3ds-form\" action=\"https://sandbox-api.iyzipay.com/payment/mock/init3ds\" method=\"post\">"
                            + "<input type=\"hidden\" name=\"orderId\" value=\"mock-1&amp;2\"><input type='hidden' name='paymentId' value='26'>"
                            + "</form><script>document.getElementById('iyzico-3ds-form').submit();</script></body></html>";

        var form = IyzicoPaymentProvider.ParseForm(html);

        Assert.NotNull(form);
        Assert.Equal("https://sandbox-api.iyzipay.com/payment/mock/init3ds", form.Action);
        Assert.Equal("mock-1&2", form.Fields["orderId"]);
        Assert.Equal("26", form.Fields["paymentId"]);
    }

    /// <summary>D13 A1: iptal ödemenin tamamını geri verir; kalem iadesinde (Partial) hiç denenmez, yalnız tutar kadar v2 iade gider.</summary>
    [Theory]
    [InlineData(true, "/v2/payment/refund")]
    [InlineData(false, "/payment/cancel")]
    public async Task Kismi_iadede_iptal_denenmez_tam_iadede_once_iptal(bool partial, string firstPath)
    {
        var handler = new RecordingHandler();
        var provider = new IyzicoPaymentProvider(
            new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new HerYerde.Business.IyzicoSettings
            {
                ApiKey = "anahtar",
                SecretKey = "gizli",
                BaseUrl = "https://sandbox-api.iyzipay.com"
            }));

        var result = await provider.RefundAsync(new HerYerde.Business.Dtos.PaymentRefundRequest("pay-1", "conv-1", 450m, "10.0.0.1", partial));

        Assert.True(result.Success);
        Assert.Equal(firstPath, handler.Paths[0]);
        Assert.Single(handler.Paths);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"success\"}")
            });
        }
    }

    [Theory]
    [InlineData("<p>form yok</p>")]
    [InlineData("<form action=\"http://banka.test/3d\" method=\"post\"></form>")]
    public void Formsuz_ya_da_https_olmayan_yanit_reddedilir(string html)
        => Assert.Null(IyzicoPaymentProvider.ParseForm(html));

    /// <summary>D14 B4: İyzico sepet satırlarının toplamının price'a eşit olmasını ister; kupon indirimi
    /// satırlara dağıtılmazsa sağlayıcı isteği reddeder.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(150)]
    [InlineData(219)]
    public async Task Kuponlu_siparisde_sepet_satirlari_toplami_odenecek_tutara_esit(int discount)
    {
        var handler = new BodyHandler();
        var provider = new IyzicoPaymentProvider(
            new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new HerYerde.Business.IyzicoSettings
            {
                ApiKey = "anahtar",
                SecretKey = "gizli",
                BaseUrl = "https://sandbox-api.iyzipay.com"
            }));

        var order = new HerYerde.Entities.Concrete.Order
        {
            OrderNo = "HY-20260116-0001",
            FullName = "Ayşe Yılmaz",
            Phone = "05424970982",
            Email = "ayse@ornek.test",
            Address = "Cumhuriyet Mah. 12/3",
            City = "İstanbul",
            District = "Kadıköy",
            Subtotal = 2190m,
            ShippingFee = 79.90m,
            Discount = discount,
            Total = 2190m + 79.90m - discount
        };
        var items = new List<HerYerde.Entities.Concrete.OrderItem>
        {
            new() { Id = 1, ProductName = "Çelik Tencere", Quantity = 1, UnitPrice = 1500m },
            new() { Id = 2, ProductName = "Cam Sürahi", Quantity = 2, UnitPrice = 345m },
            new() { Id = 3, ProductName = "Hediye Kaşık", Quantity = 1, UnitPrice = 0m, IsGift = true }
        };

        await provider.InitThreeDsAsync(new HerYerde.Business.Dtos.PaymentInitRequest(
            order,
            items,
            "conv-1",
            new HerYerde.Business.Dtos.PaymentCard("AYSE YILMAZ", "5528790000000008", "12", "2030", "123"),
            "10.0.0.1",
            "https://heryerde.test/odeme/3d-donus"));

        var body = System.Text.Json.Nodes.JsonNode.Parse(handler.Body!)!;
        var basket = (System.Text.Json.Nodes.JsonArray)body["basketItems"]!;
        var sum = basket.Sum(i => decimal.Parse((string)i!["price"]!, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(order.Total, sum);
        Assert.Equal(order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), (string)body["price"]!);
        // Hiçbir satır sıfır ya da eksi olamaz: İyzico böyle bir satırı reddeder.
        Assert.All(basket, i => Assert.True(decimal.Parse((string)i!["price"]!, System.Globalization.CultureInfo.InvariantCulture) > 0m));
    }

    private sealed class BodyHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"failure\",\"errorMessage\":\"test\"}")
            };
        }
    }
}
