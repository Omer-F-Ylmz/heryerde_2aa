using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;
using HerYerde.Tests.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HerYerde.Tests.Business;

/// <summary>KAPANIŞ-5: denetim izi bir yıl tutulur; gece işi daha eski satırı siler.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuditCleanupTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Yili_dolan_denetim_satiri_silinir_dolmayan_kalir()
    {
        await using (var setup = TestDb.NewContext())
        {
            var dal = new EfAdminAuditLogDal(setup);
            await dal.AddAsync(Entry(TestClock.Now.AddDays(-366)));
            await dal.AddAsync(Entry(TestClock.Now.AddDays(-364)));
            await new EfUnitOfWork(setup).SaveChangesAsync();
        }

        int removed;
        await using (var job = TestDb.NewContext())
        {
            var manager = new AdminAuditManager(new EfAdminAuditLogDal(job), new EfUnitOfWork(job), TestClock.Fixed);
            removed = await manager.PurgeOlderThanAsync(TimeSpan.FromDays(365));
        }

        Assert.Equal(1, removed);
        await using var check = TestDb.NewContext();
        var left = Assert.Single(await new EfAdminAuditLogDal(check).GetListAsync());
        Assert.Equal(TestClock.Now.AddDays(-364), left.At);
    }

    [Fact]
    public void Denetim_temizligi_gece_isi_olarak_kayitlidir()
    {
        using var factory = new AdminWebFactory();

        var hosted = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name);

        Assert.Contains("AuditLogCleanupHostedService", hosted);
    }

    private static AdminAuditLog Entry(DateTime at) => new()
    {
        AdminId = 1,
        Action = "durum: Onaylandı",
        Entity = "sipariş",
        EntityId = 1,
        At = at
    };
}
