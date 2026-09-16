using HerYerde.Business.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace HerYerde.Tests.Web;

/// <summary>D14: taksit tablosunun sayfalarda çizilmesi. Sağlayıcı sahte; İyzico'ya ağ çağrısı yok.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class InstallmentPageTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Urun_sayfasi_one_cikan_taksitleri_gosterir()
    {
        using var factory = new InstallmentFactory();
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 1000m, stock: 5);

        var html = await (await factory.CreateNonRedirectingClient().GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();

        Assert.Contains("Taksit seçenekleri", html);
        Assert.Contains("3 taksit", html);
        Assert.Contains("346,67", html);
        Assert.Contains("9 taksit", html);
        // Genel tabloda yalnız öne çıkanlar var.
        Assert.DoesNotContain("12 taksit", html);
    }

    [Fact]
    public async Task Anahtar_yokken_urun_sayfasinda_taksit_cizilmez()
    {
        using var factory = new AdminWebFactory();
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 1000m, stock: 5);

        var html = await (await factory.CreateNonRedirectingClient().GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Taksit seçenekleri", html);
        Assert.DoesNotContain("installments", html);
    }

    [Fact]
    public async Task Odeme_adiminda_bin_ile_bankanin_tablosu_gelir()
    {
        using var factory = new InstallmentFactory();
        await using var context = TestDb.NewContext();
        var client = factory.CreateNonRedirectingClient();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 1000m, stock: 5);
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });

        var html = await (await client.GetAsync("/odeme/taksit?bin=552879")).Content.ReadAsStringAsync();

        Assert.Contains("Test Bankası taksitleri", html);
        // Ödeme adımında tüm taksitler görünür; tablo kargo dahil sepet toplamı üzerinden.
        Assert.Contains("12 taksit", html);
        Assert.Equal("552879", factory.Provider.InstallmentBins[^1]);
        Assert.Equal(1000m + TestData.ShippingFee, factory.Provider.InstallmentPrices[^1]);
    }

    private sealed class InstallmentFactory : AdminWebFactory
    {
        public FakePaymentProvider Provider { get; } = new();

        protected override void Configure(Dictionary<string, string?> settings)
        {
            settings["Iyzico:ApiKey"] = "test-anahtar";
            settings["Iyzico:SecretKey"] = "test-gizli";
            settings["Iyzico:Installments"] = "true";
        }

        protected override void ConfigureServices(IServiceCollection services)
            => services.AddSingleton<IPaymentProvider>(Provider);
    }
}
