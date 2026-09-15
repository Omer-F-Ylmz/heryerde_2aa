using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>YAYIN-HAZIRLIK: ETBİS kayıt numarası gelince altbilgide bant olarak görünür; Legal:EtbisNo boşken iz yok.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class EtbisFooterTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Etbis_no_bosken_altbilgide_bant_yok()
    {
        using var factory = new AdminWebFactory();

        var html = await HomeAsync(factory);

        Assert.DoesNotContain("store-footer__etbis", html);
        Assert.DoesNotContain("ETBİS", html);
    }

    [Fact]
    public async Task Etbis_no_doluyken_altbilgide_bant_gorunur()
    {
        using var factory = new EtbisFactory();

        var html = await HomeAsync(factory);

        Assert.Contains("class=\"store-footer__etbis\"", html);
        Assert.Contains("ETBİS", html);
        Assert.Contains(EtbisFactory.Number, html);
    }

    private static async Task<string> HomeAsync(AdminWebFactory factory)
    {
        var response = await factory.CreateNonRedirectingClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private sealed class EtbisFactory : AdminWebFactory
    {
        public const string Number = "7F3A9C21B4E8";

        protected override void Configure(Dictionary<string, string?> settings) => settings["Legal:EtbisNo"] = Number;
    }
}

/// <summary>YAYIN-HAZIRLIK regresyon: üretimde Seed:Catalog açık bırakılsa da açılış kataloğu yazılmaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductionSeedTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Uretimde_katalog_tohumu_cagrilmaz()
    {
        using var factory = new SeedOnProductionFactory();

        var response = await factory.CreateNonRedirectingClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = TestDb.NewContext();
        Assert.Empty(context.Categories);
        Assert.Empty(context.Products);
    }

    private sealed class SeedOnProductionFactory : AdminWebFactory
    {
        protected override string Environment => "Production";

        protected override void Configure(Dictionary<string, string?> settings)
        {
            settings["AllowedHosts"] = ProductionFactory.Host;
            settings["Seed:Catalog"] = "true";
        }
    }
}
