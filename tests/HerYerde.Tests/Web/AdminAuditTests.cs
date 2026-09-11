using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Concrete;

namespace HerYerde.Tests.Web;

/// <summary>G07: yönetici işlemleri denetim izine yazılır; /admin/denetim yalnız yöneticiye açık.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminAuditTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Admin_urun_guncellemesi_denetim_satiri_yazar()
    {
        var client = await _factory.CreateSignedInClientAsync();
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        var categoryId = (await new EfProductDal(context).GetAsync(p => p.Id == productId))!.CategoryId;

        var response = await HtmlForm.PostAsync(client, $"/admin/products/edit/{productId}", "/admin/products/edit", new Dictionary<string, string>
        {
            ["Id"] = productId.ToString(),
            ["Name"] = "Çelik Tencere 24 cm",
            ["Description"] = "Çok katmanlı taban.",
            ["CategoryId"] = categoryId.ToString(),
            ["Price"] = "500.00",
            ["IsActive"] = "true"
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var entry = Assert.Single(await new EfAdminAuditLogDal(context).GetListAsync());
        Assert.Equal(await AdminIdAsync(context), entry.AdminId);
        Assert.Equal("güncelle", entry.Action);
        Assert.Equal("ürün", entry.Entity);
        Assert.Equal(productId, entry.EntityId);
        Assert.Equal(TestClock.Now, entry.At);
    }

    [Fact]
    public async Task Parola_degisimi_denetim_satiri_yazar()
    {
        var client = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(client, "/admin/sifre", "/admin/sifre", new Dictionary<string, string>
        {
            ["CurrentPassword"] = AdminWebFactory.AdminPassword,
            ["NewPassword"] = "YeniParola9x",
            ["ConfirmPassword"] = "YeniParola9x"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = TestDb.NewContext();
        var adminId = await AdminIdAsync(context);
        var entry = Assert.Single(await new EfAdminAuditLogDal(context).GetListAsync());
        Assert.Equal(adminId, entry.AdminId);
        Assert.Equal("parola değiştir", entry.Action);
        Assert.Equal("yönetici", entry.Entity);
        Assert.Equal(adminId, entry.EntityId);
    }

    [Fact]
    public async Task Anonim_denetim_listesi_giris_sayfasina_yonlendirilir()
    {
        var response = await _factory.CreateNonRedirectingClient().GetAsync("/admin/denetim");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/admin/auth/login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Denetim_listesi_son_200_kaydi_50_serlik_sayfalarla_gosterir()
    {
        var client = await _factory.CreateSignedInClientAsync();
        await using var context = TestDb.NewContext();
        context.AdminAuditLogs.AddRange(Enumerable.Range(1, 205).Select(i => new AdminAuditLog
        {
            AdminId = 1,
            Action = "güncelle",
            Entity = "ürün",
            EntityId = i,
            At = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
            Ip = "127.0.0.1"
        }));
        await context.SaveChangesAsync();

        var first = await (await client.GetAsync("/admin/denetim")).Content.ReadAsStringAsync();
        var last = await (await client.GetAsync("/admin/denetim?sayfa=4")).Content.ReadAsStringAsync();

        Assert.Contains("Sayfa 1 / 4", first);
        Assert.Contains(">#205<", first);
        Assert.Contains(">#156<", first);
        Assert.DoesNotContain(">#155<", first);
        Assert.Contains(">#6<", last);
        Assert.DoesNotContain(">#5<", last);
    }

    private static async Task<int> AdminIdAsync(HerYerde.DataAccess.Concrete.EntityFramework.Contexts.HerYerdeContext context)
        => (await new EfAdminUserDal(context).GetAsync(a => a.Email == AdminWebFactory.AdminEmail))!.Id;
}
