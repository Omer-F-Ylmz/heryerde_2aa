using System.Linq.Expressions;
using System.Net;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

/// <summary>KAPANIŞ-3 ZAP: aynı adla eşzamanlı iki kayıtta slug denetimi ikisinde de boş görür, ikinci kayıt benzersiz indekse
/// çarpar ve yönetim 500 dönüyordu (ZAP 40018/90022). Yarış, denetimin eski veri görmesiyle belirlenimli canlandırılır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CategorySlugRaceTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Denetimden_sonra_ayni_slug_yazilmissa_kayit_409_doner_istisna_firlatmaz()
    {
        await using (var seed = TestDb.NewContext())
        {
            seed.Categories.Add(new Category { Name = "Zap", Slug = "zap", SortOrder = 1, IsActive = true });
            await seed.SaveChangesAsync();
        }

        await using var context = TestDb.NewContext();
        var manager = new CategoryManager(
            new StaleCheckCategoryDal(new EfCategoryDal(context)),
            new EfProductDal(context),
            new EfSlugHistoryDal(context),
            new EfUnitOfWork(context));

        var (status, result) = await manager.AddAsync(new Category { Name = "Zap", SortOrder = 2, IsActive = true });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("yeniden deneyin", result.Message);
    }

    /// <summary>Slug denetimi (tek kayıt sorgusu) eşzamanlı isteğin kaydını henüz görmemiş gibi boş döner.</summary>
    private sealed class StaleCheckCategoryDal(ICategoryDal inner) : ICategoryDal
    {
        public Task<Category?> GetAsync(Expression<Func<Category, bool>> filter, CancellationToken cancellationToken = default)
            => Task.FromResult<Category?>(null);

        public Task<Category?> GetTrackedAsync(Expression<Func<Category, bool>> filter, CancellationToken cancellationToken = default)
            => inner.GetTrackedAsync(filter, cancellationToken);

        public Task<List<Category>> GetListAsync(Expression<Func<Category, bool>>? filter = null, CancellationToken cancellationToken = default)
            => inner.GetListAsync(filter, cancellationToken);

        public Task AddAsync(Category entity, CancellationToken cancellationToken = default) => inner.AddAsync(entity, cancellationToken);

        public void Update(Category entity) => inner.Update(entity);

        public void Delete(Category entity) => inner.Delete(entity);
    }
}
