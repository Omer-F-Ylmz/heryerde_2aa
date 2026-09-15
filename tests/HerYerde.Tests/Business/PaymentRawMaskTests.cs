using System.Text.Json.Nodes;
using HerYerde.Business.Concrete;

namespace HerYerde.Tests.Business;

/// <summary>İYZİCO-FIX: saklanan sağlayıcı yanıtında yalnız kart alanları maskelenir; kayıt geçerli JSON kalır.</summary>
public sealed class PaymentRawMaskTests
{
    // Sandbox auth yanıtının biçimi (tutarlar 8 ondalık) + en kötü durum: kart alanlarının yankılanması.
    private const string Raw =
        "{\"status\":\"success\",\"locale\":\"tr\",\"systemTime\":1757928000000,\"conversationId\":\"959a83108e1543568a1234567890abcd\","
        + "\"price\":2269.90000000,\"paidPrice\":2269.90000000,\"paymentId\":\"37812250\",\"binNumber\":\"552879\",\"lastFourDigits\":\"0008\","
        + "\"basketId\":\"HY-20260915-0030\",\"currency\":\"TRY\",\"cardNumber\":\"5528790000000008\",\"cvc\":\"123\",\"cardHolderName\":\"AYSE YILMAZ\","
        + "\"paymentCard\":{\"cardNumber\":\"5528790000000008\",\"cvc\":\"123\"},\"itemTransactions\":[{\"itemId\":\"57\",\"price\":2190.00000000}]}";

    [Fact]
    public void Maskelenmis_yanit_gecerli_json_ve_kart_bilgisi_icermez()
    {
        var masked = PaymentManager.MaskRaw(Raw)!;

        var json = JsonNode.Parse(masked)!.AsObject();
        Assert.DoesNotContain("5528790000000008", masked);
        Assert.DoesNotContain("AYSE YILMAZ", masked);
        Assert.Equal("************0008", (string?)json["cardNumber"]);
        Assert.Equal("**2879", (string?)json["binNumber"]);
        Assert.Equal("***", (string?)json["cvc"]);
        Assert.Equal("***", (string?)json["cardHolderName"]);
        Assert.Equal("************0008", (string?)json["paymentCard"]!["cardNumber"]);
        Assert.Equal("***", (string?)json["paymentCard"]!["cvc"]);
    }

    [Fact]
    public void BasketId_systemTime_conversationId_ve_tutarlar_korunur()
    {
        var json = JsonNode.Parse(PaymentManager.MaskRaw(Raw)!)!.AsObject();

        Assert.Equal("HY-20260915-0030", (string?)json["basketId"]);
        Assert.Equal("959a83108e1543568a1234567890abcd", (string?)json["conversationId"]);
        Assert.Equal("1757928000000", json["systemTime"]!.ToJsonString());
        Assert.Equal("2269.90000000", json["paidPrice"]!.ToJsonString());
        Assert.Equal("0008", (string?)json["lastFourDigits"]);
    }

    [Fact]
    public void Sinir_asan_yanit_kirpilinca_da_gecerli_json_kalir()
    {
        var items = string.Join(',', Enumerable.Range(1, 80).Select(i => $"{{\"itemId\":\"{i}\",\"paymentTransactionId\":\"3973327{i}\",\"price\":10.00000000}}"));
        var raw = Raw.Replace("[{\"itemId\":\"57\",\"price\":2190.00000000}]", "[" + items + "]");
        Assert.True(raw.Length > PaymentManager.RawResponseLimit);

        var masked = PaymentManager.MaskRaw(raw)!;

        Assert.True(masked.Length <= PaymentManager.RawResponseLimit);
        var json = JsonNode.Parse(masked)!.AsObject();
        Assert.Equal("success", (string?)json["status"]);
        Assert.Equal("37812250", (string?)json["paymentId"]);
        Assert.Equal("HY-20260915-0030", (string?)json["basketId"]);
        Assert.DoesNotContain("5528790000000008", masked);
    }
}
