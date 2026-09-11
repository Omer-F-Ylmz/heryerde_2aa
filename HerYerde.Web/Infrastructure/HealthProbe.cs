namespace HerYerde.Web.Infrastructure;

/// <summary>Konteyner sağlık denetimi: imajda curl/wget yok (aspnet:10.0, Ubuntu 24.04). Aynı dll
/// "--healthcheck" ile açılır, /health'e tek istek atar ve sonucu çıkış koduyla bildirir.</summary>
public static class HealthProbe
{
    public const string Argument = "--healthcheck";

    /// <summary>Host süzgecinden geçsin diye izinli ilk alan adı; joker ya da boşsa localhost.</summary>
    public static string HostFor(string? allowedHosts)
        => allowedHosts?
               .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
               .FirstOrDefault(host => !host.StartsWith('*'))
           ?? "localhost";

    public static async Task<int> RunAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:8080/health");
        request.Headers.Host = HostFor(Environment.GetEnvironmentVariable("AllowedHosts"));
        try
        {
            using var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
