using System.Net;
using HerYerde.Business.Concrete;
using HerYerde.DataAccess.Concrete.EntityFramework;

namespace HerYerde.Tests.Business;

[Collection(DatabaseCollection.Name)]
public sealed class AdminAuthManagerTests : IAsyncLifetime
{
    private const string Email = "admin@heryerde.test";
    private const string Password = "HerYerde!Test1";

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static AdminAuthManager NewManager(HerYerde.DataAccess.Concrete.EntityFramework.Contexts.HerYerdeContext context)
        => new(new EfAdminUserDal(context), new EfUnitOfWork(context));

    [Fact]
    public async Task Bes_hatali_denemeden_sonra_dogru_parola_bile_kilide_takilir()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        await manager.EnsureSeedAsync(Email, Password);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var (failedStatus, _) = await manager.SignInAsync(Email, "yanlis-parola");
            Assert.Equal(HttpStatusCode.Unauthorized, failedStatus);
        }

        var (status, result) = await manager.SignInAsync(Email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Dorduncu_hatadan_sonra_dogru_parola_hala_girer_ve_sayac_sifirlanir()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        await manager.EnsureSeedAsync(Email, Password);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await manager.SignInAsync(Email, "yanlis-parola");
        }

        var (status, result) = await manager.SignInAsync(Email, Password);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, result.Data!.FailedAttempts);
        Assert.Null(result.Data.LockedUntil);
    }

    [Fact]
    public async Task Kilit_suresi_dolmussa_dogru_parola_yeniden_girer()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);
        await manager.EnsureSeedAsync(Email, Password);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await manager.SignInAsync(Email, "yanlis-parola");
        }

        var dal = new EfAdminUserDal(context);
        var locked = await dal.GetTrackedAsync(a => a.Email == Email);
        locked!.LockedUntil = DateTime.UtcNow.AddMinutes(-1);
        dal.Update(locked);
        await new EfUnitOfWork(context).SaveChangesAsync();

        var (status, _) = await manager.SignInAsync(Email, Password);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task Tohumlama_ayni_e_postayi_ikinci_kez_olusturmaz()
    {
        await using var context = TestDb.NewContext();
        var manager = NewManager(context);

        var (firstStatus, _) = await manager.EnsureSeedAsync(Email, Password);
        var (secondStatus, _) = await manager.EnsureSeedAsync(Email, "BaskaParola!2");

        Assert.Equal(HttpStatusCode.Created, firstStatus);
        Assert.Equal(HttpStatusCode.OK, secondStatus);
        Assert.Single(await new EfAdminUserDal(context).GetListAsync());
    }
}
