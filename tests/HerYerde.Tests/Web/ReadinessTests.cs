using System.Net;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>YAYIN-HAZIRLIK: /health/ready veritabanı, uploads yazılabilirliği ve bildirim kuyruğu gecikmesini (&lt;10 dk)
/// sınar; anonimdir, hız sınırına takılmaz, gövdesi kısadır. /health canlılık olarak kalır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReadinessTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Hazirlik_saglikliyken_200_ve_kisa_govde_doner()
    {
        using var factory = new AdminWebFactory();

        var response = await factory.CreateNonRedirectingClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(body.Length <= 20, body);
    }

    [Fact]
    public async Task Hazirlik_veritabani_kopukken_503_canlilik_200()
    {
        using var factory = new DatabaseDownFactory();
        var client = factory.CreateNonRedirectingClient();

        var ready = await client.GetAsync("/health/ready");
        var live = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Hazirlik_uploads_yazilamiyorsa_503()
    {
        var blocker = Path.GetTempFileName();
        try
        {
            using var factory = new UploadsBlockedFactory(blocker);

            var response = await factory.CreateNonRedirectingClient().GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    [Fact]
    public async Task Outbox_gecikmesi_esikte_503()
    {
        await AddMailAsync(OutboxStatus.Bekliyor, TestClock.Now.AddMinutes(-10));
        using var factory = new AdminWebFactory();

        var response = await factory.CreateNonRedirectingClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Outbox_gecikmesi_esik_altinda_ve_gonderilmis_eski_kayitta_200()
    {
        await AddMailAsync(OutboxStatus.Bekliyor, TestClock.Now.AddMinutes(-10).AddSeconds(1));
        await AddMailAsync(OutboxStatus.Gonderildi, TestClock.Now.AddDays(-3));
        using var factory = new AdminWebFactory();

        var response = await factory.CreateNonRedirectingClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Hazirlik_hiz_sinirina_takilmaz()
    {
        using var factory = new TinyGeneralLimitFactory();
        var client = factory.CreateNonRedirectingClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        }

        await client.GetAsync("/sepet");
        await client.GetAsync("/sepet");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/sepet")).StatusCode);
    }

    private static async Task AddMailAsync(OutboxStatus status, DateTime createdAt)
    {
        await using var context = TestDb.NewContext();
        context.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxType.OrderPlaced,
            To = "ayse@example.com",
            Subject = "Siparişiniz alındı",
            Body = "gövde",
            Status = status,
            CreatedAt = createdAt
        });
        await context.SaveChangesAsync();
    }

    /// <summary>Ulaşılamayan sunucu; yönetici tohumu kapalı olduğundan uygulama veritabanına dokunmadan açılır.</summary>
    private sealed class DatabaseDownFactory : AdminWebFactory
    {
        protected override void Configure(Dictionary<string, string?> settings)
        {
            settings["ConnectionStrings:Default"] =
                "Server=127.0.0.1,1;Database=Yok;User Id=yok;Password=yok;Connect Timeout=2;ConnectRetryCount=0;TrustServerCertificate=True";
            settings["Admin:Email"] = "";
        }
    }

    /// <summary>Uploads kökü bir dosya: altına klasör açılamaz.</summary>
    private sealed class UploadsBlockedFactory(string file) : AdminWebFactory
    {
        protected override void Configure(Dictionary<string, string?> settings) => settings["Uploads:Root"] = file;
    }

    private sealed class TinyGeneralLimitFactory : AdminWebFactory
    {
        protected override void Configure(Dictionary<string, string?> settings) => settings["RateLimit:GeneralPerMinute"] = "2";
    }
}
