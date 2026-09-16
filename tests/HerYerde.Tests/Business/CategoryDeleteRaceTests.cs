using System.Linq.Expressions;
using System.Net;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

/// <summary>D16 A3 (D15 ZAP kalanı): kategori silinirken "üründe kullanılıyor mu" denetiminden sonra eşzamanlı istek o kategoriye
/// ürün eklerse yabancı anahtar çakışması 500 dönüyordu; beklenen 409.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CategoryDeleteRaceTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Denetimden_sonra_kategoriye_urun_eklenirse_silme_409_doner()
    {
        int categoryId;
        await using (var seed = TestDb.NewContext())
        {
            categoryId = await TestData.AddChildCategoryAsync(seed, "Mutfak", "mutfak");
        }

        await using var context = TestDb.NewContext();
        var manager = new CategoryManager(
            new RacingCategoryDal(new EfCategoryDal(context), categoryId),
            new EfProductDal(context),
            new EfSlugHistoryDal(context),
            new EfUnitOfWork(context));

        var (status, result) = await manager.DeleteAsync(categoryId);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("ürün", result.Message);
    }

    /// <summary>Silme işaretlendiği anda (denetimler geçmiş) başka bağlam kategoriye ürün kaydeder.</summary>
    private sealed class RacingCategoryDal(ICategoryDal inner, int categoryId) : ICategoryDal
    {
        public void Delete(Category entity)
        {
            using (var other = TestDb.NewContext())
            {
                var product = TestData.NewProduct(categoryId, "Eşzamanlı Tava", "eszamanli-tava");
                other.Products.Add(product);
                other.SaveChanges();
            }

            inner.Delete(entity);
        }

        public Task<Category?> GetAsync(Expression<Func<Category, bool>> filter, CancellationToken cancellationToken = default)
            => inner.GetAsync(filter, cancellationToken);

        public Task<Category?> GetTrackedAsync(Expression<Func<Category, bool>> filter, CancellationToken cancellationToken = default)
            => inner.GetTrackedAsync(filter, cancellationToken);

        public Task<List<Category>> GetListAsync(Expression<Func<Category, bool>>? filter = null, CancellationToken cancellationToken = default)
            => inner.GetListAsync(filter, cancellationToken);

        public Task AddAsync(Category entity, CancellationToken cancellationToken = default) => inner.AddAsync(entity, cancellationToken);

        public void Update(Category entity) => inner.Update(entity);
    }
}
