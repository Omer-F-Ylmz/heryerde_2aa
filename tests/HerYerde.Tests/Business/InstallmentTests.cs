using HerYerde.Business;
using HerYerde.Business.Concrete;
using HerYerde.Business.Dtos;
using Microsoft.Extensions.Options;

namespace HerYerde.Tests.Business;

/// <summary>D14: taksit tablosu. Sağlayıcı sahte; İyzico'ya ağ çağrısı yok.</summary>
public sealed class InstallmentTests
{
    private const decimal Price = 1000m;

    [Fact]
    public async Task Taksit_tablosu_uc_ve_alti_taksit_tutarlarini_verir()
    {
        var provider = new FakePaymentProvider();
        var manager = NewManager(provider);

        var table = await manager.GetAsync(Price);

        Assert.NotNull(table);
        var three = table.Options.Single(o => o.Count == 3);
        var six = table.Options.Single(o => o.Count == 6);
        Assert.Equal(1040.00m, three.TotalPrice);
        Assert.Equal(346.67m, three.MonthlyPrice);
        Assert.Equal(1100.00m, six.TotalPrice);
        Assert.Equal(183.33m, six.MonthlyPrice);
    }

    [Fact]
    public async Task Anahtar_yokken_taksit_sorulmaz()
    {
        var provider = new FakePaymentProvider();
        var manager = NewManager(provider, new IyzicoSettings { Installments = true });

        var table = await manager.GetAsync(Price);

        Assert.False(manager.Enabled);
        Assert.Null(table);
        Assert.Equal(0, provider.InstallmentCalls);
    }

    [Fact]
    public async Task Ikinci_istek_onbellekten_gelir_saglayiciya_gidilmez()
    {
        var provider = new FakePaymentProvider();
        var manager = NewManager(provider);

        var first = await manager.GetAsync(Price);
        var second = await manager.GetAsync(Price);

        Assert.Equal(1, provider.InstallmentCalls);
        Assert.Equal(first!.Options.Count, second!.Options.Count);
    }

    [Fact]
    public async Task Onbellek_bir_saat_sonra_saglayiciya_yeniden_sorar()
    {
        var provider = new FakePaymentProvider();
        var clock = TestClock.Movable();
        var manager = NewManager(provider, clock: clock);

        await manager.GetAsync(Price);
        clock.Advance(TimeSpan.FromMinutes(59));
        await manager.GetAsync(Price);
        clock.Advance(TimeSpan.FromMinutes(2));
        await manager.GetAsync(Price);

        Assert.Equal(2, provider.InstallmentCalls);
    }

    /// <summary>İYZİCO-DOĞRULAMA-2: gerçek sandbox hesabı taksit anlaşması yoksa yalnız tek çekim döner.
    /// Tek satırlık "tablo" fiyatı tekrarlamaktan başka bir şey söylemez; çizilmemeli.</summary>
    [Fact]
    public async Task Yalniz_tek_cekim_donerse_tablo_bos_sayilir()
    {
        var provider = new FakePaymentProvider { SingleInstallmentOnly = true };
        var manager = NewManager(provider);

        var table = await manager.GetAsync(Price);

        Assert.NotNull(table);
        Assert.False(table.HasInstallments);
        Assert.Equal(1, Assert.Single(table.Options).Count);
    }

    [Fact]
    public async Task Taksitli_tabloda_taksit_var_sayilir()
    {
        var table = await NewManager(new FakePaymentProvider()).GetAsync(Price);

        Assert.True(table!.HasInstallments);
    }

    /// <summary>YAYIN-KAPI: BIN'siz istekte sağlayıcı banka başına tablo döner; genel tablo, öne çıkan taksitlerde en düşük aylık
    /// tutarı veren bankanınkidir (sağlayıcının ilk sırası değil).</summary>
    [Fact]
    public async Task Genel_tablo_iki_bankadan_en_dusuk_taksit_tutarli_bankayi_secer()
    {
        var provider = new FakePaymentProvider
        {
            BankTables = [BankTable("Pahalı Bank", "Pahalı Kart", 0.03m), BankTable("Uygun Bank", "Uygun Kart", 0.01m)]
        };
        var manager = NewManager(provider);

        var table = await manager.GetAsync(Price);

        Assert.NotNull(table);
        Assert.Equal("Uygun Bank", table.BankName);
        Assert.Equal(1080.00m, table.Options.Single(o => o.Count == 9).TotalPrice);
        Assert.Equal(120.00m, table.Options.Single(o => o.Count == 9).MonthlyPrice);
    }

    private static InstallmentTable BankTable(string bank, string family, decimal ratePerInstallment)
        => new(
            new[] { 1, 3, 6, 9 }
                .Select(count =>
                {
                    var total = Math.Round(Price * (1m + ratePerInstallment * (count - 1)), 2);
                    return new InstallmentOption(count, Math.Round(total / count, 2), total);
                })
                .ToList(),
            bank,
            family);

    private static InstallmentManager NewManager(
        FakePaymentProvider provider,
        IyzicoSettings? settings = null,
        TimeProvider? clock = null)
        => new(provider, new InstallmentCache(), Options.Create(settings ?? Configured()), clock ?? TestClock.Fixed);

    private static IyzicoSettings Configured() => new()
    {
        ApiKey = "test-anahtar",
        SecretKey = "test-gizli",
        Installments = true
    };
}
