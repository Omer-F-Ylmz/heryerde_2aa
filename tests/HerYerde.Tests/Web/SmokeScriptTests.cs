using System.Diagnostics;

namespace HerYerde.Tests.Web;

/// <summary>YAYIN-HAZIRLIK: tools/smoke.ps1 gerçek soket dinleyen (Kestrel) üretim ayarlı sunucuya karşı tüm denetimleri geçer.
/// Aynı betik CI'da ve yayın sonrası canlı adrese karşı koşar.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SmokeScriptTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Smoke_betigi_test_sunucusuna_karsi_hepsi_gecer()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
        }

        using var factory = new KestrelProductionFactory();
        factory.UseKestrel(0);
        factory.StartServer();
        var baseUrl = factory.CreateClient().BaseAddress!.GetLeftPart(UriPartial.Authority);

        var (exitCode, output) = await RunAsync(baseUrl);

        Assert.True(exitCode == 0, output);
        Assert.Contains("SMOKE OK", output);
        Assert.DoesNotContain("FAIL", output);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string baseUrl)
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[]
                 {
                     "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                     "-File", RepoFile.PathOf("tools", "smoke.ps1"), "-BaseUrl", baseUrl
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await stdout + await stderr);
    }

    private sealed class KestrelProductionFactory : AdminWebFactory
    {
        protected override string Environment => "Production";

        protected override void Configure(Dictionary<string, string?> settings)
            => settings["AllowedHosts"] = "localhost;127.0.0.1";
    }
}
