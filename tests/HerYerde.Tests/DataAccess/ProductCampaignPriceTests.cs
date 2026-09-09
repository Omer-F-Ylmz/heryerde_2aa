using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.DataAccess;

[Collection(DatabaseCollection.Name)]
public sealed class ProductCampaignPriceTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Kampanya_fiyati_fiyata_esit_veya_buyukse_check_constraint_reddeder()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context);
        var dal = new EfProductDal(context);
        var unitOfWork = new EfUnitOfWork(context);

        var product = TestData.NewProduct(categoryId, "Şile Bezi Şalvar", "sile-bezi-salvar");
        product.CampaignPrice = product.Price;
        product.CampaignLabel = "Açılışa özel";
        await dal.AddAsync(product);

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task Kampanya_fiyati_fiyattan_kucukse_kaydedilir()
    {
        await using var context = TestDb.NewContext();
        var categoryId = await TestData.AddRootCategoryAsync(context);
        var dal = new EfProductDal(context);
        var unitOfWork = new EfUnitOfWork(context);

        var product = TestData.NewProduct(categoryId, "Şile Bezi Şalvar", "sile-bezi-salvar");
        product.CampaignPrice = 399m;
        product.CampaignLabel = "Açılışa özel";
        product.CampaignEndsAt = DateTime.UtcNow.AddDays(7);
        await dal.AddAsync(product);
        await unitOfWork.SaveChangesAsync();

        var saved = await dal.GetAsync(p => p.Slug == "sile-bezi-salvar");

        Assert.NotNull(saved);
        Assert.Equal(399m, saved.CampaignPrice);
    }
}
