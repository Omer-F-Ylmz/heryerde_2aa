using System.Net;
using System.Net.Http.Headers;
using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace HerYerde.Tests.Web;

/// <summary>D10 A4: kategori görseli yönetimden yüklenir (16:9 + 1:1 webp); kapı kartı, sekme ve başlık bandı görselli,
/// görselsiz kategoride placeholder deseni kalır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CategoryImageTests : IAsyncLifetime
{
    private readonly UploadFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Kategori_gorseli_yuklenir_iki_oranda_uretilir_ve_vitrinde_gorunur()
    {
        int evId;
        int childId;
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            evId = (await context.Categories.SingleAsync(c => c.Slug == "ev")).Id;
            childId = await TestData.AddChildCategoryAsync(context, "Mutfak", "mutfak");
        }

        var client = await _factory.CreateSignedInClientAsync();
        var rootUpload = await UploadAsync(client, evId, "Ev", TestImage.Png(1600, 1000));
        var childUpload = await UploadAsync(client, childId, "Mutfak", TestImage.Png(900, 900), parentId: evId);

        Assert.Equal(HttpStatusCode.Found, rootUpload.StatusCode);
        Assert.Equal(HttpStatusCode.Found, childUpload.StatusCode);

        string rootUrl;
        string childUrl;
        await using (var context = TestDb.NewContext())
        {
            rootUrl = (await new EfCategoryDal(context).GetAsync(c => c.Id == evId))!.ImageUrl!;
            childUrl = (await new EfCategoryDal(context).GetAsync(c => c.Id == childId))!.ImageUrl!;
        }

        Assert.Matches($"^/uploads/categories/{evId}/[0-9a-f]{{32}}-16x9\\.webp$", rootUrl);
        using (var wide = await Image.LoadAsync(Path.Combine(_factory.Root, rootUrl.TrimStart('/'))))
        using (var square = await Image.LoadAsync(Path.Combine(_factory.Root, rootUrl.Replace("-16x9.webp", "-1x1.webp").TrimStart('/'))))
        {
            Assert.Equal((1200, 675), (wide.Width, wide.Height));
            Assert.Equal((800, 800), (square.Width, square.Height));
        }

        var anonymous = _factory.CreateNonRedirectingClient();
        var home = await (await anonymous.GetAsync("/")).Content.ReadAsStringAsync();
        var listing = await (await anonymous.GetAsync("/ev")).Content.ReadAsStringAsync();

        Assert.Contains($"src=\"{rootUrl}\"", home);
        Assert.Contains($"src=\"{rootUrl}\"", listing);
        Assert.Contains($"src=\"{childUrl.Replace("-16x9.webp", "-1x1.webp")}\"", listing);
    }

    [Fact]
    public async Task Gorselsiz_kategoride_kapi_karti_ve_bant_placeholder_gosterir()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        var client = _factory.CreateNonRedirectingClient();
        var home = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        var listing = await (await client.GetAsync("/ev")).Content.ReadAsStringAsync();

        Assert.Contains("door__media", home);
        Assert.DoesNotContain("/uploads/categories/", home);
        Assert.Matches("class=\"category-band__media\">\\s*<span class=\"ph", listing);
    }

    [Fact]
    public async Task Gorsel_olmayan_icerik_kategori_formunda_400()
    {
        int evId;
        await using (var context = TestDb.NewContext())
        {
            evId = await TestData.AddRootCategoryAsync(context, "Ev", "ev");
        }

        var client = await _factory.CreateSignedInClientAsync();
        var response = await UploadAsync(client, evId, "Ev", [0x4D, 0x5A, 0x90, 0x00]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Null((await new EfCategoryDal(check).GetAsync(c => c.Id == evId))!.ImageUrl);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, int id, string name, byte[] bytes, int? parentId = null)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(client, $"/admin/categories/edit/{id}");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(id.ToString()), "Id" },
            { new StringContent(name), "Name" },
            { new StringContent("1"), "SortOrder" },
            { new StringContent("true"), "IsActive" },
            { new StringContent(parentId is null ? "true" : "false"), "IsRoot" }
        };
        if (parentId is { } parent)
        {
            content.Add(new StringContent(parent.ToString()), "ParentId");
        }

        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "Image", "kategori.png");
        return await client.PostAsync("/admin/categories/edit", content);
    }
}
