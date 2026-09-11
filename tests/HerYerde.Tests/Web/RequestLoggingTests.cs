using System.Collections.Concurrent;
using System.Net;
using HerYerde.Entities.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace HerYerde.Tests.Web;

/// <summary>G07: istek logu yazılır, telefon/e-posta/adres loga düşmez, correlation id taşınır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class RequestLoggingTests : IAsyncLifetime
{
    private const string Phone = "0542 497 09 82";
    private const string Email = "ayse.yilmaz@example.com";
    private const string Address = "Cumhuriyet Mah. 12/3";

    private readonly LogCaptureFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Istek_logunda_telefon_eposta_ve_adres_yer_almaz()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var client = _factory.CreateNonRedirectingClient();
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });

        var placed = await HtmlForm.PostAsync(client, "/odeme", "/odeme", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["Phone"] = Phone,
            ["Email"] = Email,
            ["Address"] = Address,
            ["City"] = "İstanbul",
            ["District"] = "Kadıköy",
            ["PaymentMethod"] = ((int)PaymentMethod.KapidaOdeme).ToString()
        });
        await placed.Content.ReadAsStringAsync();
        await (await client.GetAsync("/ara?q=" + Uri.EscapeDataString(Email))).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Found, placed.StatusCode);
        Assert.Contains(_factory.Sink.Events, e => IsRequestLog(e) && e.Properties["RequestPath"].ToString() == "\"/odeme\"");
        var dump = Dump(_factory.Sink.Events);
        Assert.DoesNotContain(Phone, dump);
        Assert.DoesNotContain("5424970982", dump);
        Assert.DoesNotContain(Email, dump);
        Assert.DoesNotContain(Address, dump);
    }

    [Fact]
    public void Pii_adli_log_alanlari_maskelenir()
    {
        var logger = _factory.Services.GetRequiredService<ILogger<RequestLoggingTests>>();

        logger.LogInformation("Sipariş {Phone} {Email} {Address} {City}", Phone, Email, Address, "İstanbul");

        var dump = Dump(_factory.Sink.Events);
        Assert.Contains("İstanbul", dump);
        Assert.DoesNotContain(Phone, dump);
        Assert.DoesNotContain(Email, dump);
        Assert.DoesNotContain(Address, dump);
    }

    [Fact]
    public async Task Correlation_id_yanitta_ve_istek_logunda_tasinir()
    {
        var client = _factory.CreateNonRedirectingClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/robots.txt");
        request.Headers.Add("X-Correlation-ID", "test-akis-42");

        var response = await client.SendAsync(request);
        await response.Content.ReadAsStringAsync();
        var other = await client.GetAsync("/robots.txt");
        await other.Content.ReadAsStringAsync();

        Assert.Equal("test-akis-42", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Contains(_factory.Sink.Events, e => IsRequestLog(e)
            && e.Properties.TryGetValue("CorrelationId", out var id) && id.ToString() == "\"test-akis-42\"");
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(other.Headers.GetValues("X-Correlation-ID"))));
    }

    private static bool IsRequestLog(LogEvent e) => e.MessageTemplate.Text.StartsWith("HTTP ");

    private static string Dump(IEnumerable<LogEvent> events)
        => string.Join('\n', events.Select(e => e.RenderMessage() + " " + string.Join(' ', e.Properties.Values)));
}

/// <summary>Uygulamanın Serilog hattına bellekte toplayan bir sink ekler.</summary>
public sealed class LogCaptureFactory : AdminWebFactory
{
    public CollectingSink Sink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(Sink));
    }
}

public sealed class CollectingSink : ILogEventSink
{
    public ConcurrentQueue<LogEvent> Events { get; } = new();

    public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
}
