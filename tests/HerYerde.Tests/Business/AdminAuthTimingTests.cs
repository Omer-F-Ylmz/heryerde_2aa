using System.Diagnostics;
using System.Net;

namespace HerYerde.Tests.Business;

/// <summary>S16: bilinmeyen e-posta bilinen e-postadan ayırt edilmemeli — ne mesajla ne süreyle.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminAuthTimingTests : IAsyncLifetime
{
    private const string Email = "admin@heryerde.test";
    private const string Password = "HerYerde!Test1";

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Kilit_mesaji_hatali_giris_mesajiyla_ayni_metindir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewAdminAuthManager(context);
        await manager.EnsureSeedAsync(Email, Password);

        var (_, wrong) = await manager.SignInAsync(Email, "yanlis-parola");
        for (var attempt = 2; attempt <= 5; attempt++)
        {
            await manager.SignInAsync(Email, "yanlis-parola");
        }

        var (lockedStatus, locked) = await manager.SignInAsync(Email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, lockedStatus);
        Assert.Equal(wrong.Message, locked.Message);
    }

    [Fact]
    public async Task Bilinmeyen_e_posta_bilinenle_ayni_mesaji_verir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewAdminAuthManager(context);
        await manager.EnsureSeedAsync(Email, Password);

        var (unknownStatus, unknown) = await manager.SignInAsync("yok@heryerde.test", Password);
        var (_, known) = await manager.SignInAsync(Email, "yanlis-parola");

        Assert.Equal(HttpStatusCode.Unauthorized, unknownStatus);
        Assert.Equal(known.Message, unknown.Message);
    }

    [Fact]
    public async Task Bilinmeyen_ve_bilinen_e_posta_yanit_sureleri_yakindir()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewAdminAuthManager(context);
        await manager.EnsureSeedAsync(Email, Password);

        // Bilinen hesapta doğru parola kullanılıyor: hatalı deneme sayacı 5'i aşıp hesabı kilitlerse
        // kilit yolu hash yapmadan döner ve ölçüm anlamsızlaşır.
        // Isınma: ilk çağrılar JIT'i, hash algoritmasını ve sorgu planını ısıtır.
        for (var i = 0; i < 3; i++)
        {
            await manager.SignInAsync("isinma@heryerde.test", Password);
            await manager.SignInAsync(Email, Password);
        }

        var unknown = await MedianMillisecondsAsync(() => manager.SignInAsync("yok@heryerde.test", Password));
        var known = await MedianMillisecondsAsync(() => manager.SignInAsync(Email, Password));

        Assert.True(
            Math.Abs(unknown - known) < 20,
            $"Bilinmeyen {unknown:0.0} ms, bilinen {known:0.0} ms; fark 20 ms'in altında olmalı.");
    }

    private static async Task<double> MedianMillisecondsAsync(Func<Task> call)
    {
        var samples = new List<double>();
        for (var i = 0; i < 7; i++)
        {
            var watch = Stopwatch.StartNew();
            await call();
            watch.Stop();
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        return samples[samples.Count / 2];
    }
}
