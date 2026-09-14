using System.Net;

namespace HerYerde.Tests.Business;

/// <summary>D5-B4: ithal edilen ürün price=1 ile gelir; fiyat girilmeden yayına alınamaz.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PriceMissingTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Fiyati_girilmemis_urun_yayina_alinamaz(decimal price)
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", price: price);
        var product = (await new HerYerde.DataAccess.Concrete.EntityFramework.EfProductDal(context).GetAsync(p => p.Id == productId))!;
        product.IsActive = true;

        var (status, result) = await TestData.NewProductManager(context).UpdateAsync(product);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("fiyat", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fiyat_girilince_yayina_alinir()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Hasır Sepet", "hasir-sepet", price: 1m);
        var product = (await new HerYerde.DataAccess.Concrete.EntityFramework.EfProductDal(context).GetAsync(p => p.Id == productId))!;
        product.IsActive = true;
        product.Price = 249.90m;

        var (status, _) = await TestData.NewProductManager(context).UpdateAsync(product);

        Assert.Equal(HttpStatusCode.OK, status);
    }
}
