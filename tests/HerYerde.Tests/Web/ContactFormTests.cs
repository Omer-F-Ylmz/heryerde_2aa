using System.Net;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D9: /iletisim formu kaydeder, mağazaya outbox'la haber verir; bot ve sel korumalı; admin listesi.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ContactFormTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Bos_alanli_form_400_doner_ve_kayit_acmaz()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await PostAsync(client, new Dictionary<string, string>
        {
            ["Name"] = "",
            ["Contact"] = "",
            ["Subject"] = ((int)ContactSubject.Siparis).ToString(),
            ["Message"] = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("field__error", await response.Content.ReadAsStringAsync());
        await using var context = TestDb.NewContext();
        Assert.Equal(0, await context.ContactMessages.CountAsync());
    }

    [Fact]
    public async Task Honeypot_dolu_form_sessizce_200_doner_kayit_ve_posta_yok()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await PostAsync(client, Valid(website: "http://spam.example"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Mesajınız bize ulaştı", await response.Content.ReadAsStringAsync());
        await using var context = TestDb.NewContext();
        Assert.Equal(0, await context.ContactMessages.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Gecerli_mesaj_kaydedilir_ve_magazaya_outbox_kaydi_duser()
    {
        var client = _factory.CreateNonRedirectingClient();

        var response = await PostAsync(client, Valid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Mesajınız bize ulaştı", await response.Content.ReadAsStringAsync());
        await using var context = TestDb.NewContext();
        var message = Assert.Single(await context.ContactMessages.ToListAsync());
        Assert.Equal("Ayşe Yılmaz", message.Name);
        Assert.Equal("0542 497 09 82", message.Contact);
        Assert.Equal(ContactSubject.Iade, message.Subject);
        Assert.Equal(TestClock.Now, message.CreatedAt);
        Assert.Null(message.ReadAt);

        var mail = Assert.Single(await context.OutboxMessages.ToListAsync());
        Assert.Equal(OutboxType.ContactMessage, mail.Type);
        Assert.Equal(TestData.StoreEmail, mail.To);
        Assert.Equal(OutboxStatus.Bekliyor, mail.Status);
        Assert.Contains("Ürün kırık geldi", mail.Body);
    }

    [Fact]
    public async Task Posta_govdesi_mesajdaki_html_i_kacislar()
    {
        var client = _factory.CreateNonRedirectingClient();
        var fields = Valid();
        fields["Message"] = "<img src=x onerror=alert(1)> merhaba";

        await PostAsync(client, fields);

        await using var context = TestDb.NewContext();
        var mail = Assert.Single(await context.OutboxMessages.ToListAsync());
        Assert.DoesNotContain("<img src=x", mail.Body);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", mail.Body);
    }

    /// <summary>KAPANIŞ-2 S-CRLF: ad posta konusuna girer; satır sonu konuya taşınmaz (başlık enjeksiyonu yüzeyi).</summary>
    [Fact]
    public async Task Addaki_satir_sonu_posta_konusuna_girmez()
    {
        var client = _factory.CreateNonRedirectingClient();
        var fields = Valid();
        fields["Name"] = "Ali\r\nBcc: kurban@example.com";

        await PostAsync(client, fields);

        await using var context = TestDb.NewContext();
        var mail = Assert.Single(await context.OutboxMessages.ToListAsync());
        Assert.DoesNotContain('\r', mail.Subject);
        Assert.DoesNotContain('\n', mail.Subject);
        Assert.EndsWith("Ali Bcc: kurban@example.com", mail.Subject);
    }

    /// <summary>KAPANIŞ-2 V-RET: bir yılı dolan iletişim mesajı gece işinde silinir, dolmayan kalır.</summary>
    [Fact]
    public async Task Yili_dolan_iletisim_mesaji_silinir_dolmayan_kalir()
    {
        await using (var setup = TestDb.NewContext())
        {
            setup.ContactMessages.Add(new ContactMessage { Name = "Eski", Contact = "e@x", Message = "eski", CreatedAt = TestClock.Now.AddDays(-366) });
            setup.ContactMessages.Add(new ContactMessage { Name = "Yeni", Contact = "y@x", Message = "yeni", CreatedAt = TestClock.Now.AddDays(-364) });
            await setup.SaveChangesAsync();
        }

        int removed;
        await using (var job = TestDb.NewContext())
        {
            removed = await TestData.NewContactManager(job).PurgeOlderThanAsync(TimeSpan.FromDays(365));
        }

        Assert.Equal(1, removed);
        await using var check = TestDb.NewContext();
        Assert.Equal("Yeni", (await check.ContactMessages.SingleAsync()).Name);
    }

    [Fact]
    public async Task Ayni_IP_den_altinci_mesaj_429_alir()
    {
        var client = _factory.CreateNonRedirectingClient();
        var token = await HtmlForm.AntiforgeryTokenAsync(client, "/iletisim");

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var allowed = await client.PostAsync("/iletisim", Form(Valid(), token));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var blocked = await client.PostAsync("/iletisim", Form(Valid(), token));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Anonim_mesaj_listesi_giris_sayfasina_yonlendirilir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/admin/mesajlar");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/admin/auth/login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Admin_mesaji_okundu_isaretler_ve_siler_denetim_izi_yazilir()
    {
        await using var context = TestDb.NewContext();
        var message = new ContactMessage
        {
            Name = "Merve T.",
            Contact = "merve@example.com",
            Subject = ContactSubject.Urun,
            Message = "Sürahinin ölçüsü nedir?",
            CreatedAt = TestClock.Now
        };
        context.ContactMessages.Add(message);
        await context.SaveChangesAsync();
        var admin = await _factory.CreateSignedInClientAsync();

        var list = await (await admin.GetAsync("/admin/mesajlar")).Content.ReadAsStringAsync();
        Assert.Contains("Sürahinin ölçüsü nedir?", list);
        Assert.Contains("merve@example.com", list);

        var read = await HtmlForm.PostAsync(admin, "/admin/mesajlar", $"/admin/mesajlar/{message.Id}/okundu", []);
        Assert.Equal(HttpStatusCode.Found, read.StatusCode);
        await using (var check = TestDb.NewContext())
        {
            Assert.Equal(TestClock.Now, (await check.ContactMessages.SingleAsync()).ReadAt);
        }

        var deleted = await HtmlForm.PostAsync(admin, "/admin/mesajlar", $"/admin/mesajlar/{message.Id}/sil", []);
        Assert.Equal(HttpStatusCode.Found, deleted.StatusCode);

        await using var after = TestDb.NewContext();
        Assert.Equal(0, await after.ContactMessages.CountAsync());
        var trail = await after.AdminAuditLogs.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(["okundu", "sil"], trail.Select(a => a.Action));
        Assert.All(trail, a => Assert.Equal("mesaj", a.Entity));
        Assert.All(trail, a => Assert.Equal(message.Id, a.EntityId));
    }

    private static Dictionary<string, string> Valid(string website = "") => new()
    {
        ["Name"] = "Ayşe Yılmaz",
        ["Contact"] = "0542 497 09 82",
        ["Subject"] = ((int)ContactSubject.Iade).ToString(),
        ["Message"] = "Ürün kırık geldi, nasıl iade edebilirim?",
        ["Website"] = website
    };

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Dictionary<string, string> fields)
        => await client.PostAsync("/iletisim", Form(fields, await HtmlForm.AntiforgeryTokenAsync(client, "/iletisim")));

    private static FormUrlEncodedContent Form(Dictionary<string, string> fields, string token)
        => new(new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token });
}
