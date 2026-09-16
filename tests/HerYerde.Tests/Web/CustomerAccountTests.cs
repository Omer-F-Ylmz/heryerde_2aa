using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HerYerde.Business;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using static HerYerde.Tests.Web.CustomerAuthTests;

namespace HerYerde.Tests.Web;

/// <summary>D16 B: hesap sayfası — adres defteri (il/ilçe, varsayılan ödeme formunu doldurur), sipariş geçmişi ve siparişe
/// customer_id, favori kalpleri, yorumlarım, çeyiz listelerim, hesabı silme (anonimleştirme hattı), KVKK aracı ve yönetim
/// sipariş detayında üye bağlantısı, yasal metinler.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CustomerAccountTests : IAsyncLifetime
{
    private readonly PrivateRootFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Adres_defteri_ilceyi_ile_gore_dogrular_varsayilan_adres_odeme_formunu_doldurur()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = await SignedInAsync(_factory);

        var wrongDistrict = await AddAddressAsync(client, "Ev", "Kadıköy", city: "Ankara");
        Assert.Equal(HttpStatusCode.BadRequest, wrongDistrict.StatusCode);

        Assert.Equal(HttpStatusCode.Found, (await AddAddressAsync(client, "İş", "Beşiktaş", isDefault: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await AddAddressAsync(client, "Ev", "Kadıköy", isDefault: true)).StatusCode);
        var list = await (await client.GetAsync("/hesap/adresler")).Content.ReadAsStringAsync();
        Assert.Contains("Beşiktaş", list);
        Assert.Contains("Varsayılan", list);

        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new() { ["productId"] = productId.ToString(), ["quantity"] = "1" });
        var checkout = await (await client.GetAsync("/odeme")).Content.ReadAsStringAsync();

        Assert.Equal("Ayşe Yılmaz", InputValue(checkout, "FullName"));
        Assert.Equal("0542 497 09 82", InputValue(checkout, "Phone"));
        Assert.Equal(Email, InputValue(checkout, "Email"));
        Assert.Matches("<option value=\"İstanbul\" selected=\"selected\">", checkout);
        Assert.Matches("<option value=\"Kadıköy\" selected=\"selected\">", checkout);
        Assert.Contains("Moda Cad. 5/2", checkout);
    }

    [Fact]
    public async Task Girisli_siparis_musteriye_baglanir_hesap_siparislerinde_listelenir_baskasininki_listelenmez()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var foreign = await PlaceGuestOrderAsync(context, "baska@example.com", "yabanci-urun");
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = await SignedInAsync(_factory);
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new() { ["productId"] = productId.ToString(), ["quantity"] = "1" });

        var placed = await HtmlForm.PostAsync(client, "/odeme", "/odeme", new()
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Email"] = "farkli@example.com",
            ["Address"] = "Moda Cad. 5/2",
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)PaymentMethod.KapidaOdeme).ToString(),
            ["LegalConsent"] = "true"
        });

        Assert.Equal(HttpStatusCode.Found, placed.StatusCode);
        await using var check = TestDb.NewContext();
        var customerId = (await check.Customers.AsNoTracking().SingleAsync()).Id;
        var order = await check.Orders.AsNoTracking().SingleAsync(o => o.Id != foreign);
        Assert.Equal(customerId, order.CustomerId);
        var foreignNo = (await check.Orders.AsNoTracking().SingleAsync(o => o.Id == foreign)).OrderNo;

        var account = await (await client.GetAsync("/hesap")).Content.ReadAsStringAsync();
        Assert.Contains(order.OrderNo, account);
        Assert.Contains($"/siparis/{order.OrderNo}/tesekkur?t={order.AccessToken}", account);
        Assert.DoesNotContain(foreignNo, account);
    }

    [Fact]
    public async Task Girissiz_favori_kalbi_giris_sayfasina_urune_donusle_goturur()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = _factory.CreateNonRedirectingClient();

        var product = await (await client.GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();
        var listing = await (await client.GetAsync("/ev")).Content.ReadAsStringAsync();

        Assert.Matches("<a class=\"fav[^\"]*\" href=\"/hesap/giris\\?donus=%2Furun%2Fcelik-tencere\"[^>]*aria-label=\"Favorilere eklemek için giriş yapın\"", product);
        Assert.Matches("<a class=\"fav[^\"]*\" href=\"/hesap/giris\\?donus=%2Fev\"", listing);
    }

    [Fact]
    public async Task Girisli_favori_eklenir_kaldirilir_kart_ve_urun_sayfasinda_isaretli_favoriler_sayfasinda_listelenir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = await SignedInAsync(_factory);

        var added = await HtmlForm.PostAsync(client, "/urun/celik-tencere", $"/hesap/favoriler/{productId}", new() { ["donus"] = "/urun/celik-tencere" });

        Assert.Equal(HttpStatusCode.SeeOther, added.StatusCode);
        Assert.Equal("/urun/celik-tencere", added.Headers.Location!.OriginalString);
        var product = await (await client.GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();
        var listing = await (await client.GetAsync("/ev")).Content.ReadAsStringAsync();
        var favorites = await (await client.GetAsync("/hesap/favoriler")).Content.ReadAsStringAsync();
        Assert.Matches($"<button class=\"fav[^\"]*\"[^>]*aria-pressed=\"true\"[^>]*data-favorite=\"{productId}\"", product);
        Assert.Matches($"<button class=\"fav[^\"]*\"[^>]*aria-pressed=\"true\"[^>]*data-favorite=\"{productId}\"", listing);
        Assert.Contains("Çelik Tencere", favorites);

        var token = await HtmlForm.AntiforgeryTokenAsync(client, "/urun/celik-tencere");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/hesap/favoriler/{productId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token })
        };
        request.Headers.Accept.ParseAdd("application/json");
        var removed = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var json = JsonDocument.Parse(await removed.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("favorite").GetBoolean());
        await using var check = TestDb.NewContext();
        Assert.Empty(await check.CustomerFavorites.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Girisli_yorum_ve_ceyiz_listesi_musteriye_baglanir_hesapta_listelenir()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password);
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = await SignedInAsync(_factory);

        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/urun/celik-tencere/yorum", new()
        {
            ["Name"] = "Ayşe Y.",
            ["Rating"] = "5",
            ["Comment"] = "Tabanı kalın, yemek dibine tutmuyor."
        });
        var created = await HtmlForm.PostAsync(client, "/ceyizlistesi/yeni", "/ceyizlistesi/yeni", new()
        {
            ["OwnerName"] = "Ayşe & Mehmet",
            ["Phone"] = "0542 497 09 82",
            ["EventDate"] = "2026-06-01",
            ["IsPublic"] = "true"
        });
        Assert.Equal(HttpStatusCode.Found, created.StatusCode);

        await using var check = TestDb.NewContext();
        var customerId = (await check.Customers.AsNoTracking().SingleAsync()).Id;
        var registry = await check.GiftRegistries.AsNoTracking().SingleAsync();
        Assert.Equal(customerId, (await check.ProductReviews.AsNoTracking().SingleAsync()).CustomerId);
        Assert.Equal(customerId, registry.CustomerId);

        var reviews = await (await client.GetAsync("/hesap/yorumlar")).Content.ReadAsStringAsync();
        var registries = await (await client.GetAsync("/hesap/ceyiz-listeleri")).Content.ReadAsStringAsync();
        Assert.Contains("Tabanı kalın", reviews);
        Assert.Contains("Onay bekliyor", reviews);
        Assert.Contains("Ayşe &amp; Mehmet", registries);
        Assert.Contains($"/ceyizlistesi/yonet?t={registry.ManageToken}", registries);
    }

    [Fact]
    public async Task Hesap_silme_acik_siparis_varken_409_doner_hicbir_sey_silinmez()
    {
        await using var context = TestDb.NewContext();
        var customerId = await TestData.AddCustomerAsync(context, Email, Password);
        var open = await PlaceGuestOrderAsync(context, Email, "acik-urun");
        await context.Orders.Where(o => o.Id == open).ExecuteUpdateAsync(s => s.SetProperty(o => o.CustomerId, customerId));
        var client = await SignedInAsync(_factory);

        var response = await HtmlForm.PostAsync(client, "/hesap/sil", "/hesap/sil", new() { ["Email"] = Email });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Contains((await check.Orders.AsNoTracking().SingleAsync()).OrderNo, await response.Content.ReadAsStringAsync());
        Assert.Single(await check.Customers.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Hesap_silme_onay_epostasi_uymazsa_400_uyarsa_kapali_siparisleri_anonimlestirir_hesap_verisini_siler_oturumu_kapatir()
    {
        await using var context = TestDb.NewContext();
        var customerId = await TestData.AddCustomerAsync(context, Email, Password);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var delivered = await PlaceGuestOrderAsync(context, Email, "teslim-urun");
        await context.Orders.Where(o => o.Id == delivered).ExecuteUpdateAsync(s => s
            .SetProperty(o => o.CustomerId, customerId)
            .SetProperty(o => o.Status, OrderStatus.TeslimEdildi));
        var client = await SignedInAsync(_factory);
        await AddAddressAsync(client, "Ev", "Kadıköy", isDefault: true);
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", $"/hesap/favoriler/{productId}", []);
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/urun/celik-tencere/yorum", new()
        {
            ["Name"] = "Ayşe Yılmaz",
            ["Rating"] = "4",
            ["Comment"] = "Güzel tencere, kapağı sıkı."
        });

        var mismatch = await HtmlForm.PostAsync(client, "/hesap/sil", "/hesap/sil", new() { ["Email"] = "baska@example.com" });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);

        var deleted = await HtmlForm.PostAsync(client, "/hesap/sil", "/hesap/sil", new() { ["Email"] = Email });

        Assert.Equal(HttpStatusCode.Found, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await client.GetAsync("/hesap")).StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Empty(await check.Customers.AsNoTracking().ToListAsync());
        Assert.Empty(await check.CustomerAddresses.AsNoTracking().ToListAsync());
        Assert.Empty(await check.CustomerFavorites.AsNoTracking().ToListAsync());
        var order = await check.Orders.AsNoTracking().SingleAsync(o => o.Id == delivered);
        Assert.Equal("A*** Y***", order.FullName);
        Assert.Null(order.CustomerId);
        var review = await check.ProductReviews.AsNoTracking().SingleAsync();
        Assert.Equal("A*** Y***", review.Name);
        Assert.Null(review.CustomerId);
    }

    [Fact]
    public async Task Kvkk_araci_uye_hesabini_bulur_dokume_ekler_tumunu_anonimlestir_hesabi_siler()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddCustomerAsync(context, Email, Password, phone: "05424970982");
        var client = await SignedInAsync(_factory);
        await AddAddressAsync(client, "Ev", "Kadıköy", isDefault: true);
        var admin = await _factory.CreateSignedInClientAsync();

        var found = await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk", new() { ["ara"] = "0542 497 09 82" });
        var json = await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk/dokum-json", new() { ["ara"] = Email });

        Assert.Contains("Üye hesabı", await found.Content.ReadAsStringAsync());
        using (var document = JsonDocument.Parse(await json.Content.ReadAsStringAsync()))
        {
            Assert.Equal(Email, document.RootElement.GetProperty("account").GetProperty("email").GetString());
            Assert.Equal(1, document.RootElement.GetProperty("addresses").GetArrayLength());
        }

        var anonymized = await HtmlForm.PostAsync(admin, "/admin/kvkk", "/admin/kvkk/anonimlestir", new() { ["ara"] = Email });

        Assert.Equal(HttpStatusCode.OK, anonymized.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Empty(await check.Customers.AsNoTracking().ToListAsync());
        Assert.Empty(await check.CustomerAddresses.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Yonetim_siparis_detayinda_uye_baglantisi_gorunur_misafirde_gorunmez()
    {
        await using var context = TestDb.NewContext();
        var customerId = await TestData.AddCustomerAsync(context, Email, Password);
        var member = await PlaceGuestOrderAsync(context, Email, "uye-urun");
        var guest = await PlaceGuestOrderAsync(context, "misafir@example.com", "misafir-urun");
        await context.Orders.Where(o => o.Id == member).ExecuteUpdateAsync(s => s.SetProperty(o => o.CustomerId, customerId));
        var admin = await _factory.CreateSignedInClientAsync();

        var memberHtml = await (await admin.GetAsync($"/admin/orders/detail/{member}")).Content.ReadAsStringAsync();
        var guestHtml = await (await admin.GetAsync($"/admin/orders/detail/{guest}")).Content.ReadAsStringAsync();

        // KVKK aracı kişiyi POST ile arar (e-posta adres satırına girmez): bağlantı gizli alanlı form.
        Assert.Contains("Üye hesabı", memberHtml);
        Assert.Matches("<form method=\"post\" action=\"/admin/kvkk\"[^>]*>(?:(?!</form>)[\\s\\S])*name=\"ara\" value=\"" + Regex.Escape(Email) + "\"", memberHtml);
        Assert.DoesNotContain("Üye hesabı", guestHtml);
    }

    [Fact]
    public async Task Yasal_metinler_envanter_ve_altbilgi_uye_hesabini_kapsar()
    {
        var client = _factory.CreateNonRedirectingClient();
        var cookies = await (await client.GetAsync("/yasal/cerez-politikasi")).Content.ReadAsStringAsync();
        var kvkk = await (await client.GetAsync("/yasal/kvkk-aydinlatma")).Content.ReadAsStringAsync();
        var privacy = await (await client.GetAsync("/yasal/gizlilik-politikasi")).Content.ReadAsStringAsync();
        var inventory = RepoFile.ReadAllText("docs", "veri-envanteri.md");

        Assert.Contains("heryerde.customer", cookies);
        Assert.Contains("Üye hesabı", kvkk);
        Assert.Contains("pazarlama", kvkk);
        Assert.DoesNotContain("Üyelik yoktur.", privacy);
        Assert.Contains("`customer`", inventory);
        Assert.Contains("`customer_address`", inventory);
        Assert.Contains("`customer_favorite`", inventory);
        Assert.Contains("href=\"/hesap/sil\"", cookies);
        Assert.NotEqual("2026-09-16.3", LegalDocs.Version);
    }

    private static Task<HttpResponseMessage> AddAddressAsync(HttpClient client, string title, string district, bool isDefault = false, string city = "İstanbul")
    {
        var fields = new Dictionary<string, string>
        {
            ["Title"] = title,
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = "0542 497 09 82",
            ["Address"] = title == "Ev" ? "Moda Cad. 5/2" : "Barbaros Bulvarı 10",
            ["City"] = city,
            ["District"] = district
        };
        if (isDefault)
        {
            fields["IsDefault"] = "true";
        }

        return HtmlForm.PostAsync(client, "/hesap/adresler/yeni", "/hesap/adresler/yeni", fields);
    }

    private static string? InputValue(string html, string name)
    {
        var tag = Regex.Match(html, $"<input[^>]*\\bname=\"{name}\"[^>]*>");
        return tag.Success ? Regex.Match(tag.Value, "\\bvalue=\"([^\"]*)\"").Groups[1].Value : null;
    }
}
