using System.Linq.Expressions;
using System.Net;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Abstract;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Business;

/// <summary>D15 ZAP (yönetim taraması, 40018/90022): ürün ve marka formuna eşzamanlı istekte benzersiz indeks çakışması 500
/// dönüyordu — ürün yeniden adlandırmasında slug geçmişi (ux_slug_history_entity_type_old_slug), markada ad (ux_brand_name).
/// Yarışlar belirlenimli canlandırılır; beklenen 409.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminSaveRaceTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ayni_urun_esanli_yeniden_adlandirilinca_ikinci_kayit_409_doner()
    {
        int productId;
        await using (var seed = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(seed, "Zap Tava", "zap-tava");
        }

        await using var context = TestDb.NewContext();
        var manager = new ProductManager(
            new EfProductDal(context),
            new EfProductVariantDal(context),
            new EfProductImageDal(context),
            new EfProductVideoDal(context),
            new EfCategoryDal(context),
            new RacingSlugHistoryDal(new EfSlugHistoryDal(context)),
            new EfUnitOfWork(context));
        var product = (await new EfProductDal(context).GetAsync(p => p.Id == productId))!;
        product.Name = "Zap Tencere";

        var (status, result) = await manager.UpdateAsync(product);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("yeniden deneyin", result.Message);
    }

    [Fact]
    public async Task Ayni_adla_esanli_marka_kaydi_409_doner_istisna_firlatmaz()
    {
        await using (var seed = TestDb.NewContext())
        {
            await TestData.AddBrandAsync(seed, "Zap", "zap");
        }

        await using var context = TestDb.NewContext();
        var manager = new BrandManager(new StaleCheckBrandDal(new EfBrandDal(context)), new EfProductDal(context), new EfUnitOfWork(context));

        var (status, result) = await manager.SaveAsync(new Brand { Name = "Zap" });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("yeniden deneyin", result.Message);
    }

    /// <summary>Eski adres satırı eklendikten sonra, kaydetmeden önce eşzamanlı istek aynı ürünü başka ada çevirip kaydeder:
    /// ikisi de geçmişte eski adresi boş görmüştür.</summary>
    private sealed class RacingSlugHistoryDal(ISlugHistoryDal inner) : ISlugHistoryDal
    {
        public async Task RecordAsync(string entityType, int entityId, string oldSlug, string newSlug, DateTime at, CancellationToken cancellationToken = default)
        {
            await inner.RecordAsync(entityType, entityId, oldSlug, newSlug, at, cancellationToken);

            await using var other = TestDb.NewContext();
            var concurrent = (await new EfProductDal(other).GetAsync(p => p.Id == entityId, cancellationToken))!;
            concurrent.Name = "Zap Güveç";
            var (status, _) = await TestData.NewProductManager(other).UpdateAsync(concurrent, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, status);
        }

        public Task<int?> FindEntityIdAsync(string entityType, string oldSlug, CancellationToken cancellationToken = default)
            => inner.FindEntityIdAsync(entityType, oldSlug, cancellationToken);

        public Task<SlugHistory?> GetAsync(Expression<Func<SlugHistory, bool>> filter, CancellationToken cancellationToken = default)
            => inner.GetAsync(filter, cancellationToken);

        public Task<SlugHistory?> GetTrackedAsync(Expression<Func<SlugHistory, bool>> filter, CancellationToken cancellationToken = default)
            => inner.GetTrackedAsync(filter, cancellationToken);

        public Task<List<SlugHistory>> GetListAsync(Expression<Func<SlugHistory, bool>>? filter = null, CancellationToken cancellationToken = default)
            => inner.GetListAsync(filter, cancellationToken);

        public Task AddAsync(SlugHistory entity, CancellationToken cancellationToken = default) => inner.AddAsync(entity, cancellationToken);

        public void Update(SlugHistory entity) => inner.Update(entity);

        public void Delete(SlugHistory entity) => inner.Delete(entity);
    }

    /// <summary>Ad ve slug denetimi (tek kayıt sorgusu) eşzamanlı isteğin kaydını henüz görmemiş gibi boş döner.</summary>
    private sealed class StaleCheckBrandDal(IBrandDal inner) : IBrandDal
    {
        public Task<Brand?> GetAsync(Expression<Func<Brand, bool>> filter, CancellationToken cancellationToken = default)
            => Task.FromResult<Brand?>(null);

        public Task<Brand?> GetTrackedAsync(Expression<Func<Brand, bool>> filter, CancellationToken cancellationToken = default)
            => inner.GetTrackedAsync(filter, cancellationToken);

        public Task<List<Brand>> GetListAsync(Expression<Func<Brand, bool>>? filter = null, CancellationToken cancellationToken = default)
            => inner.GetListAsync(filter, cancellationToken);

        public Task AddAsync(Brand entity, CancellationToken cancellationToken = default) => inner.AddAsync(entity, cancellationToken);

        public void Update(Brand entity) => inner.Update(entity);

        public void Delete(Brand entity) => inner.Delete(entity);
    }
}
