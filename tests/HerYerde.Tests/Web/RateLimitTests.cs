using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>S05/S13: kaba kuvvet ve sepet şişirme hız sınırıyla durur.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class RateLimitTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Admin_girisinde_on_birinci_istek_429_alir()
    {
        var client = _factory.CreateNonRedirectingClient();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var allowed = await client.GetAsync("/admin/auth/login");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var blocked = await client.GetAsync("/admin/auth/login");

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Odemede_altinci_post_429_alir()
    {
        var client = _factory.CreateNonRedirectingClient();
        var token = await HtmlForm.AntiforgeryTokenAsync(client, "/admin/auth/login");

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var allowed = await PostOdemeAsync(client, token);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var blocked = await PostOdemeAsync(client, token);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Sepet_postlarinda_esik_asilinca_429_alinir()
    {
        using var factory = new LowLimitFactory();
        var client = factory.CreateNonRedirectingClient();
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
        var token = await HtmlForm.AntiforgeryTokenAsync(client, "/urun/cam-surahi");

        for (var attempt = 1; attempt <= LowLimitFactory.CartPerMinute; attempt++)
        {
            var allowed = await PostSepetAsync(client, token);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var blocked = await PostSepetAsync(client, token);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Sinira_takilan_istek_markali_sayfa_gorur()
    {
        using var factory = new LowLimitFactory();
        var client = factory.CreateNonRedirectingClient();
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
        var token = await HtmlForm.AntiforgeryTokenAsync(client, "/urun/cam-surahi");

        for (var attempt = 1; attempt <= LowLimitFactory.CartPerMinute + 1; attempt++)
        {
            await PostSepetAsync(client, token);
        }

        var blocked = await PostSepetAsync(client, token);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal("text/html", blocked.Content.Headers.ContentType!.MediaType);
        var html = await blocked.Content.ReadAsStringAsync();
        Assert.Contains("Çok fazla istek", html);
    }

    private static Task<HttpResponseMessage> PostOdemeAsync(HttpClient client, string token)
        => client.PostAsync("/odeme", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "05424970982",
            ["Address"] = "Cumhuriyet Mah. 12/3",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = "1",
            ["__RequestVerificationToken"] = token
        }));

    private static Task<HttpResponseMessage> PostSepetAsync(HttpClient client, string token)
        => client.PostAsync("/sepet/ekle", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = "1",
            ["quantity"] = "1",
            ["__RequestVerificationToken"] = token
        }));
}

/// <summary>Sepet eşiğini testte hızlıca doldurabilmek için düşürür.</summary>
public sealed class LowLimitFactory : AdminWebFactory
{
    public const int CartPerMinute = 3;

    protected override void Configure(Dictionary<string, string?> settings)
        => settings["RateLimit:CartPerMinute"] = CartPerMinute.ToString();
}
