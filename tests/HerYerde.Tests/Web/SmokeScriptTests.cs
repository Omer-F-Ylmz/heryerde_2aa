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

    /// <summary>D16 C: yayın sonrası staging'e karşı: kapı -Credential ile geçilir, -Staging noindex başlığını ve kapalı robots'u
    /// denetler; kimliksiz koşu kırmızıdır.</summary>
    [Fact]
    public async Task Smoke_betigi_staging_kapisini_kimlikle_gecer_noindex_denetler_kimliksiz_kirmizi()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
        }

        using var factory = new StagingEnvironmentTests.StagingFactory(StagingEnvironmentTests.User, StagingEnvironmentTests.Password);
        factory.UseKestrel(0);
        factory.StartServer();
        var baseUrl = factory.CreateClient().BaseAddress!.GetLeftPart(UriPartial.Authority);

        var (exitCode, output) = await RunAsync(baseUrl, "-Staging", "-Credential", $"{StagingEnvironmentTests.User}:{StagingEnvironmentTests.Password}");
        var (deniedCode, deniedOutput) = await RunAsync(baseUrl, "-Staging");

        Assert.True(exitCode == 0, output);
        Assert.Contains("OK    staging noindex", output);
        Assert.Contains("OK    staging robots kapali", output);
        Assert.Contains("SMOKE OK", output);
        Assert.Equal(1, deniedCode);
        Assert.Contains("FAIL", deniedOutput);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string baseUrl, params string[] extra)
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
                 }.Concat(extra))
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
