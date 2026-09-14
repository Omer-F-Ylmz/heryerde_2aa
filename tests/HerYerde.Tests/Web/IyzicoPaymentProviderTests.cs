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

    [Theory]
    [InlineData("<p>form yok</p>")]
    [InlineData("<form action=\"http://banka.test/3d\" method=\"post\"></form>")]
    public void Formsuz_ya_da_https_olmayan_yanit_reddedilir(string html)
        => Assert.Null(IyzicoPaymentProvider.ParseForm(html));
}
