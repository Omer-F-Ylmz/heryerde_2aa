using System.Net;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

/// <summary>S15: görsel yalnız URL metniyle geliyor; şema beyaz listesi olmadan javascript:/data: girer.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductImageUrlTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.example.com/gorsel.jpg")]
    [InlineData("gorsel.jpg")]
    [InlineData("   ")]
    public async Task Gecersiz_gorsel_adresi_reddedilir(string url)
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");

        var (status, _) = await TestData.NewProductManager(context).AddImageAsync(new ProductImage
        {
            ProductId = productId,
            Url = url,
            Alt = "Cam sürahi"
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Empty(await new HerYerde.DataAccess.Concrete.EntityFramework.EfProductImageDal(context).GetListAsync());
    }

    [Theory]
    [InlineData("https://cdn.example.com/gorsel.jpg")]
    [InlineData("http://cdn.example.com/gorsel.jpg")]
    [InlineData("/img/cam-surahi.jpg")]
    public async Task Gecerli_gorsel_adresi_kabul_edilir(string url)
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");

        var (status, _) = await TestData.NewProductManager(context).AddImageAsync(new ProductImage
        {
            ProductId = productId,
            Url = url,
            Alt = "Cam sürahi"
        });

        Assert.Equal(HttpStatusCode.Created, status);
    }
}
