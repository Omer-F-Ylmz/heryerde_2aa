using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;

namespace HerYerde.Tests.Web;

[Collection(DatabaseCollection.Name)]
public sealed class AdminCrudFlowTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Giris_yapan_yonetici_kategori_ve_urun_ekleyebilir()
    {
        var client = await _factory.CreateSignedInClientAsync();

        var categoryResponse = await HtmlForm.PostAsync(client, "/admin/categories/create", "/admin/categories/create", new Dictionary<string, string>
        {
            ["Name"] = "Ev",
            ["SortOrder"] = "1",
            ["IsActive"] = "true"
        });
        Assert.Equal(HttpStatusCode.Found, categoryResponse.StatusCode);

        await using var context = TestDb.NewContext();
        var category = await new EfCategoryDal(context).GetAsync(c => c.Slug == "ev");
        Assert.NotNull(category);

        var productResponse = await HtmlForm.PostAsync(client, "/admin/products/create", "/admin/products/create", new Dictionary<string, string>
        {
            ["Name"] = "Çelik Tencere 24 cm",
            ["Description"] = "Çok katmanlı taban.",
            ["CategoryId"] = category.Id.ToString(),
            ["Price"] = "1290.50",
            ["CampaignPrice"] = "1090.00",
            ["CampaignLabel"] = "Çeyiz indirimi",
            ["IsActive"] = "true"
        });
        Assert.Equal(HttpStatusCode.Found, productResponse.StatusCode);

        var product = await new EfProductDal(context).GetAsync(p => p.Slug == "celik-tencere-24-cm");
        Assert.NotNull(product);
        Assert.Equal(1290.50m, product.Price);
        Assert.Equal(1090.00m, product.CampaignPrice);
    }

    [Fact]
    public async Task Urunu_olan_kategorinin_silinmesi_409_ile_reddedilir()
    {
        var client = await _factory.CreateSignedInClientAsync();
        await HtmlForm.PostAsync(client, "/admin/categories/create", "/admin/categories/create", new Dictionary<string, string>
        {
            ["Name"] = "Ev",
            ["SortOrder"] = "1",
            ["IsActive"] = "true"
        });

        await using var context = TestDb.NewContext();
        var root = (await new EfCategoryDal(context).GetAsync(c => c.Slug == "ev"))!;
        await HtmlForm.PostAsync(client, "/admin/categories/create", "/admin/categories/create", new Dictionary<string, string>
        {
            ["Name"] = "Mutfak & Sofra",
            ["ParentId"] = root.Id.ToString(),
            ["SortOrder"] = "1",
            ["IsActive"] = "true"
        });
        var child = (await new EfCategoryDal(context).GetAsync(c => c.Slug == "mutfak-sofra"))!;
        await TestData.AddProductAsync(context, child.Id, "Çelik Tencere", "celik-tencere");

        var response = await HtmlForm.PostAsync(client, "/admin/categories", $"/admin/categories/delete/{child.Id}", []);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(await new EfCategoryDal(context).GetAsync(c => c.Id == child.Id));
    }
}
