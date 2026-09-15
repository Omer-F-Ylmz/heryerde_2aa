using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D10 A2: /siparis-sorgula — sipariş no + tam telefon eşleşirse token'lı sipariş sayfasına 303; aksi hâlde
/// hangi alanın yanlış olduğunu söylemeyen tek mesaj. Dakikada 10 deneme, honeypot.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderLookupTests : IAsyncLifetime
{
    private const string Phone = "0542 497 09 82";
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Siparis_no_ve_tam_telefon_eslesince_token_sayfasina_303()
    {
        var order = await PlaceAsync();
        var client = _factory.CreateNonRedirectingClient();

        var response = await LookupAsync(client, order.OrderNo, "+90 542 497 0982");

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Yanlis_telefon_ve_olmayan_siparis_ayni_mesaji_alir()
    {
        var order = await PlaceAsync();
        var client = _factory.CreateNonRedirectingClient();

        var wrongPhone = await LookupAsync(client, order.OrderNo, "0542 497 09 83");
        var partialPhone = await LookupAsync(client, order.OrderNo, "0982");
        var unknownOrder = await LookupAsync(client, "HY-20990101-9999", Phone);

        Assert.All([wrongPhone, partialPhone, unknownOrder], r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var messages = await Task.WhenAll(new[] { wrongPhone, partialPhone, unknownOrder }.Select(MessageAsync));
        Assert.Single(messages.Distinct());
        Assert.Contains("eşleşen sipariş bulunamadı", messages[0]);
    }

    [Fact]
    public async Task Sorgulamada_on_birinci_istek_429()
    {
        var client = _factory.CreateNonRedirectingClient();
        var token = await HtmlForm.AntiforgeryTokenAsync(client, "/siparis-sorgula");

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await PostAsync(client, token, "HY-20990101-0001", Phone)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync(client, token, "HY-20990101-0001", Phone)).StatusCode);
    }

    [Fact]
    public async Task Honeypot_dolu_istek_dogru_bilgide_de_yonlendirilmez()
    {
        var order = await PlaceAsync();
        var client = _factory.CreateNonRedirectingClient();

        var response = await LookupAsync(client, order.OrderNo, Phone, website: "http://spam.example");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("eşleşen sipariş bulunamadı", await MessageAsync(response));
    }

    [Fact]
    public async Task Altbilgi_ve_sss_sorgulama_sayfasina_baglanir()
    {
        var client = _factory.CreateNonRedirectingClient();

        var home = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        var faq = await (await client.GetAsync("/sss")).Content.ReadAsStringAsync();

        Assert.Contains("href=\"/siparis-sorgula\"", home);
        Assert.Contains("href=\"/siparis-sorgula\"", faq);
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(html, "<p class=\"form-alert\"[^>]*>(.*?)</p>");
        Assert.True(match.Success, "form-alert yok");
        return match.Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> LookupAsync(HttpClient client, string orderNo, string phone, string? website = null)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(client, "/siparis-sorgula");
        return await PostAsync(client, token, orderNo, phone, website);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string orderNo, string phone, string? website = null)
        => client.PostAsync("/siparis-sorgula", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["OrderNo"] = orderNo,
            ["Phone"] = phone,
            ["Website"] = website ?? "",
            ["__RequestVerificationToken"] = token
        }));

    private static async Task<Order> PlaceAsync()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var cart = TestData.NewCartManager(context);
        var cartId = (await cart.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cart.AddAsync(cartId, productId, null, 1);
        return (await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", Phone, null, "Örnek mah. 1", "İstanbul", "Kadıköy", null, PaymentMethod.KapidaOdeme))).Item2.Data!;
    }
}
