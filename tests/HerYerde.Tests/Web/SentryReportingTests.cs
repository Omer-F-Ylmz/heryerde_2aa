using System.Collections.Concurrent;
using System.Net;
using HerYerde.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace HerYerde.Tests.Web;

/// <summary>YAYIN-HAZIRLIK: hata izleme Sentry:Dsn boşken tümüyle kapalıdır; doluyken hata olayı telefon/e-posta/adres
/// maskelenmiş gider. Gönderim testte DI'daki kayıt taşıyıcısına yönlenir, ağa çıkmaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SentryReportingTests : IAsyncLifetime
{
    private const string Phone = "0542 497 09 82";

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dsn_bosken_hata_sayfasi_calisir_gonderim_yok()
    {
        var transport = new RecordingTransport();
        using (var factory = new FailingSentryFactory(dsn: "", transport))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, FailingSentryFactory.FailingPath);
            request.Headers.Accept.ParseAdd("text/html");

            var response = await factory.CreateNonRedirectingClient().SendAsync(request);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Contains("Bir şeyler ters gitti", await response.Content.ReadAsStringAsync());
            Assert.False(SentrySdk.IsEnabled);
        }

        Assert.Empty(transport.Envelopes);
    }

    [Fact]
    public async Task Dsn_doluyken_hata_gonderilir_telefon_maskelenir()
    {
        var transport = new RecordingTransport();
        using (var factory = new FailingSentryFactory(dsn: "https://anahtar@sentry.invalid/1", transport))
        {
            var response = await factory.CreateNonRedirectingClient().GetAsync(FailingSentryFactory.FailingPath);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        }

        var sent = string.Join('\n', transport.Envelopes);
        Assert.True(sent.Contains(FailingSentryFactory.MessagePrefix, StringComparison.Ordinal), $"{transport.Envelopes.Count} zarf: {sent}");
        Assert.Contains("***", sent);
        Assert.DoesNotContain(Phone, sent);
    }

    /// <summary>YAYIN-KAPI: Production'da müşteriye görünen metinde yer tutucu kaldıysa Sentry'ye uyarı düzeyinde olay gider.</summary>
    [Fact]
    public async Task Uretimde_yer_tutucu_Sentry_uyarisi_olarak_gider()
    {
        var transport = new RecordingTransport();
        using (var factory = new FailingSentryFactory(dsn: "https://anahtar@sentry.invalid/1", transport))
        {
            var response = await factory.CreateNonRedirectingClient().GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        }

        var sent = string.Join('\n', transport.Envelopes);
        Assert.True(sent.Contains("yer tutucu", StringComparison.Ordinal), $"{transport.Envelopes.Count} zarf: {sent}");
        Assert.Contains("\"level\":\"warning\"", sent);
    }

    [Fact]
    public void Before_send_eposta_metnini_ve_adres_alanini_maskeler()
    {
        var sentryEvent = new SentryEvent
        {
            Message = new SentryMessage { Formatted = "ayse.yilmaz@example.com için sipariş" }
        };
        sentryEvent.SetExtra("Address", "Cumhuriyet Mah. 12/3");
        sentryEvent.SetExtra("OrderNo", "HY-1001");

        var scrubbed = SentryReporting.Scrub(sentryEvent);

        Assert.Equal("*** için sipariş", scrubbed.Message!.Formatted);
        Assert.Equal("***", scrubbed.Extra["Address"]);
        Assert.Equal("HY-1001", scrubbed.Extra["OrderNo"]);
    }

    private sealed class RecordingTransport : ITransport
    {
        public ConcurrentQueue<string> Envelopes { get; } = new();

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            Envelopes.Enqueue(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }
    }

    /// <summary>Üretim ortamı; mesajında telefon geçen hatayı fırlatan uç.</summary>
    private sealed class FailingSentryFactory(string dsn, ITransport transport) : AdminWebFactory
    {
        public const string FailingPath = "/test/sentry-patla";
        public const string MessagePrefix = "Musteri geri aranacak";

        protected override string Environment => "Production";

        protected override void Configure(Dictionary<string, string?> settings)
        {
            settings["AllowedHosts"] = ProductionFactory.Host;
            settings["Sentry:Dsn"] = dsn;
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(transport);
            services.AddSingleton<IStartupFilter, FailingEndpoint>();
        }

        private sealed class FailingEndpoint : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                next(app);
                app.Map(FailingPath, branch => branch.Run(_ => throw new InvalidOperationException($"{MessagePrefix}: {Phone}")));
            };
        }
    }
}
