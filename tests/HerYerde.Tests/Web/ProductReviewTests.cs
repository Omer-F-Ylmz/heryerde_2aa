using System.Net;
using System.Text.RegularExpressions;
using HerYerde.Business.Dtos;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D9: ürün yorumları onaydan geçince görünür; ortalama puan, AggregateRating, ana sayfa alıntıları, admin onayı.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductReviewTests : IAsyncLifetime
{
    private const string Slug = "cam-surahi";

    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Gonderilen_yorum_onay_bekler_ve_urun_sayfasinda_gorunmez()
    {
        await AddProductAsync();
        var client = _factory.CreateNonRedirectingClient();

        var response = await PostReviewAsync(client, Review(comment: "Cam kalın, çok beğendim."));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        await using var context = TestDb.NewContext();
        var saved = Assert.Single(await context.ProductReviews.ToListAsync());
        Assert.False(saved.IsApproved);
        Assert.Equal(TestClock.Now, saved.CreatedAt);

        var html = await GetHtmlAsync(client, "/urun/" + Slug);
        Assert.DoesNotContain("Cam kalın, çok beğendim.", html);
        Assert.Contains("Yorumunuz onaylandıktan sonra yayınlanır.", html);
    }

    [Fact]
    public async Task Onayli_yorum_gorunur_ve_ortalama_puan_dogru()
    {
        var productId = await AddProductAsync();
        await AddReviewAsync(productId, "Ayşe K.", 5, "Tam fotoğraftaki gibi.", approved: true);
        await AddReviewAsync(productId, "Merve T.", 4, "Paketleme özenliydi.", approved: true);
        await AddReviewAsync(productId, "Onaysız", 1, "Bu görünmemeli.", approved: false);

        var html = await GetHtmlAsync(_factory.CreateClient(), "/urun/" + Slug);

        Assert.Contains("Tam fotoğraftaki gibi.", html);
        Assert.Contains("Paketleme özenliydi.", html);
        Assert.DoesNotContain("Bu görünmemeli.", html);
        Assert.Contains("data-rating-average=\"4,5\"", html);
        Assert.Contains("2 yorum", html);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Aralik_disi_puan_400_ile_reddedilir(int rating)
    {
        await AddProductAsync();
        var client = _factory.CreateNonRedirectingClient();

        var response = await PostReviewAsync(client, Review(rating: rating));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Equal(0, await context.ProductReviews.CountAsync());
    }

    [Fact]
    public async Task Ayni_IP_den_dorduncu_yorum_429_alir()
    {
        await AddProductAsync();
        var client = _factory.CreateNonRedirectingClient();
        var token = await HtmlForm.AntiforgeryTokenAsync(client, "/urun/" + Slug);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var allowed = await client.PostAsync($"/urun/{Slug}/yorum", Form(Review(), token));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var blocked = await client.PostAsync($"/urun/{Slug}/yorum", Form(Review(), token));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task AggregateRating_yalniz_onayli_yorum_varsa_yazilir()
    {
        var productId = await AddProductAsync();
        await AddReviewAsync(productId, "Onaysız", 2, "Beklemede.", approved: false);
        var client = _factory.CreateClient();

        var before = PageHtml.Single(PageHtml.JsonLd(await GetHtmlAsync(client, "/urun/" + Slug)), "Product");
        Assert.False(before.TryGetProperty("aggregateRating", out _));

        await AddReviewAsync(productId, "Ayşe K.", 5, "Harika.", approved: true);
        await AddReviewAsync(productId, "Fatma D.", 4, "İyi.", approved: true);

        var after = PageHtml.Single(PageHtml.JsonLd(await GetHtmlAsync(client, "/urun/" + Slug)), "Product");
        var aggregate = after.GetProperty("aggregateRating");
        Assert.Equal("AggregateRating", aggregate.GetProperty("@type").GetString());
        Assert.Equal(4.5m, aggregate.GetProperty("ratingValue").GetDecimal());
        Assert.Equal(2, aggregate.GetProperty("reviewCount").GetInt32());
        Assert.Equal(5, aggregate.GetProperty("bestRating").GetInt32());
        Assert.Equal(1, aggregate.GetProperty("worstRating").GetInt32());
    }

    [Fact]
    public async Task Ana_sayfa_onayli_yorum_varsa_onu_gosterir()
    {
        var productId = await AddProductAsync();
        await AddReviewAsync(productId, "Sevgi B.", 5, "Sürahi mutfağımın yıldızı oldu.", approved: true);
        await AddReviewAsync(productId, "Gizli", 5, "Onaysız alıntı.", approved: false);

        var html = await GetHtmlAsync(_factory.CreateClient(), "/");

        Assert.Contains("Sürahi mutfağımın yıldızı oldu.", html);
        Assert.DoesNotContain("Onaysız alıntı.", html);
        // Onaylı yorum varken sabit DM alıntıları yerini ona bırakır.
        Assert.DoesNotContain("Tencereler tam fotoğraftaki gibi geldi", html);
    }

    [Fact]
    public async Task Onayli_yorum_yoksa_ana_sayfada_yorum_bolumu_ve_sabit_alinti_yok()
    {
        var html = await GetHtmlAsync(_factory.CreateClient(), "/");

        // YAYIN-KAPI: sabit alıntılar kaldırıldı; gerçek onaylı yorum yoksa bölüm çizilmez.
        Assert.DoesNotContain("id=\"dm\"", html);
        Assert.DoesNotContain("Tencereler tam fotoğraftaki gibi geldi", html);
    }

    [Fact]
    public async Task Admin_yorum_onayi_denetim_izi_yazar_ve_yorum_yayinlanir()
    {
        var productId = await AddProductAsync();
        var reviewId = await AddReviewAsync(productId, "Ayşe K.", 5, "Onay bekleyen yorum.", approved: false);
        var admin = await _factory.CreateSignedInClientAsync();

        var list = await GetHtmlAsync(admin, "/admin/yorumlar");
        Assert.Contains("Onay bekleyen yorum.", list);

        var response = await HtmlForm.PostAsync(admin, "/admin/yorumlar", $"/admin/yorumlar/{reviewId}/onayla", []);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.True((await context.ProductReviews.SingleAsync()).IsApproved);
        var entry = Assert.Single(await context.AdminAuditLogs.ToListAsync());
        Assert.Equal("onayla", entry.Action);
        Assert.Equal("yorum", entry.Entity);
        Assert.Equal(reviewId, entry.EntityId);
        Assert.Contains("Onay bekleyen yorum.", await GetHtmlAsync(_factory.CreateClient(), "/urun/" + Slug));
    }

    [Fact]
    public async Task Admin_yorum_reddi_kaydi_siler_ve_denetim_izi_yazar()
    {
        var productId = await AddProductAsync();
        var reviewId = await AddReviewAsync(productId, "Spam", 1, "Reddedilecek.", approved: false);
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(admin, "/admin/yorumlar", $"/admin/yorumlar/{reviewId}/reddet", []);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Equal(0, await context.ProductReviews.CountAsync());
        Assert.Equal("reddet", Assert.Single(await context.AdminAuditLogs.ToListAsync()).Action);
    }

    [Fact]
    public async Task Anonim_yorum_listesi_giris_sayfasina_yonlendirilir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/admin/yorumlar");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/admin/auth/login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Yorumdaki_XSS_yuku_kacislanir()
    {
        var productId = await AddProductAsync();
        await AddReviewAsync(productId, "<b>Kötü</b>", 5, "<script>alert('xss')</script>", approved: true);

        var html = await GetHtmlAsync(_factory.CreateClient(), "/urun/" + Slug);

        Assert.DoesNotContain("<script>alert('xss')</script>", html);
        Assert.DoesNotContain("<b>Kötü</b>", html);
        Assert.Contains("&lt;script&gt;alert(&#x27;xss&#x27;)&lt;/script&gt;", html);
    }

    [Fact]
    public async Task Gecerli_siparis_numarasi_dogrulanmis_alici_rozeti_verir_uydurma_numara_vermez()
    {
        await AddProductAsync();
        var orderNo = await PlaceOrderForProductAsync();
        var client = _factory.CreateNonRedirectingClient();

        await PostReviewAsync(client, Review(name: "Alıcı", orderNo: orderNo));
        await PostReviewAsync(client, Review(name: "Uyduran", orderNo: "HY-20260115-9999"));

        await using var context = TestDb.NewContext();
        var reviews = await context.ProductReviews.OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(orderNo, reviews[0].OrderNo);
        Assert.Null(reviews[1].OrderNo);

        await context.ProductReviews.ExecuteUpdateAsync(s => s.SetProperty(r => r.IsApproved, true));
        var html = await GetHtmlAsync(client, "/urun/" + Slug);
        Assert.Single(Regex.Matches(html, "Doğrulanmış alıcı"));
    }

    private static Dictionary<string, string> Review(
        string name = "Ayşe K.",
        int rating = 5,
        string comment = "Çok memnun kaldım, teşekkürler.",
        string orderNo = "") => new()
        {
            ["Name"] = name,
            ["Rating"] = rating.ToString(),
            ["Comment"] = comment,
            ["OrderNo"] = orderNo
        };

    private static async Task<int> AddProductAsync()
    {
        await using var context = TestDb.NewContext();
        return await TestData.AddHomeProductAsync(context, "Cam Sürahi", Slug, stock: 10);
    }

    private static async Task<int> AddReviewAsync(int productId, string name, int rating, string comment, bool approved)
    {
        await using var context = TestDb.NewContext();
        var review = new ProductReview
        {
            ProductId = productId,
            Name = name,
            Rating = rating,
            Comment = comment,
            IsApproved = approved,
            CreatedAt = TestClock.Now
        };
        context.ProductReviews.Add(review);
        await context.SaveChangesAsync();
        return review.Id;
    }

    private static async Task<string> PlaceOrderForProductAsync()
    {
        await using var context = TestDb.NewContext();
        var productId = (await context.Products.SingleAsync(p => p.Slug == Slug)).Id;
        var cartManager = TestData.NewCartManager(context);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, variantId: null, quantity: 1);
        var (_, placed) = await TestData.NewOrderManager(context).PlaceAsync(cartId, new OrderDraft(
            "Ayşe Yılmaz", "0542 497 09 82", null, "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, PaymentMethod.KapidaOdeme));
        return placed.Data!.OrderNo;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<HttpResponseMessage> PostReviewAsync(HttpClient client, Dictionary<string, string> fields)
        => await client.PostAsync(
            $"/urun/{Slug}/yorum",
            Form(fields, await HtmlForm.AntiforgeryTokenAsync(client, "/urun/" + Slug)));

    private static FormUrlEncodedContent Form(Dictionary<string, string> fields, string token)
        => new(new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token });
}
