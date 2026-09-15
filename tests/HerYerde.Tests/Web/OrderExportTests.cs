using System.Net;
using System.Text;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D12 C3/C4: sipariş CSV'si Excel'in Türkçe ayarıyla doğrudan açılsın diye UTF-8 BOM ve ";" ayraçlı; kargo
/// şablonu sabit kolon sırasında. Rapor sayfası ve CSV'si tarih aralığıyla iner.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class OrderExportTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Tam_dokum_utf8_bom_ve_noktali_virgul_ayracli()
    {
        await PlaceAsync();
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync("/admin/orders/export?bicim=tam");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Sipariş No;Tarih;Durum;Kanal;Ödeme;", lines[0]);
        Assert.Contains("Ayşe Yılmaz", lines[1]);
        Assert.Contains("\"Cumhuriyet Mah. 12/3; kat 2\"", lines[1]);
    }

    [Fact]
    public async Task Kargo_sablonu_sabit_kolon_sirasinda_ve_iz_yazilir()
    {
        var order = await PlaceAsync();
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await admin.GetAsync($"/admin/orders/export?bicim=kargo&durum={(int)OrderStatus.Beklemede}&tarih=2026-01-15");
        var csv = Encoding.UTF8.GetString((await response.Content.ReadAsByteArrayAsync())[3..]);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Ad Soyad;Telefon;Adres;İl;İlçe;Tutar;Ödeme;Sipariş No;Kalemler", lines[0]);
        Assert.Equal($"Ayşe Yılmaz;05424970982;\"Cumhuriyet Mah. 12/3; kat 2\";İstanbul;Kadıköy;979,90;Kapıda ödeme;{order.OrderNo};Çelik Tencere x2", lines[1]);
        await using var context = TestDb.NewContext();
        Assert.Contains(await new EfAdminAuditLogDal(context).GetListAsync(), a => a.Action == "sipariş dışa aktarma" && a.Detail == "kargo · 1 sipariş");
    }

    [Fact]
    public async Task Rapor_sayfasi_ve_csv_tarih_araligiyla_iner()
    {
        var order = await PlaceAsync();
        await using (var context = TestDb.NewContext())
        {
            (await context.Orders.FindAsync(order.Id))!.Status = OrderStatus.TeslimEdildi;
            await context.SaveChangesAsync();
        }

        var admin = await _factory.CreateSignedInClientAsync();
        var page = await admin.GetAsync("/admin/rapor?baslangic=2026-01-01&bitis=2026-01-31&donem=gun");
        var csv = await admin.GetAsync("/admin/rapor/csv?baslangic=2026-01-01&bitis=2026-01-31&donem=gun");
        var outside = await (await admin.GetAsync("/admin/rapor?baslangic=2026-02-01&bitis=2026-02-28")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("979,90", await page.Content.ReadAsStringAsync());
        Assert.Contains("Çelik Tencere", await page.Content.ReadAsStringAsync());
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.Contains("15.01.2026;979,90;1", Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync()));
        Assert.DoesNotContain("979,90", outside);
    }

    private static async Task<HerYerde.Entities.Concrete.Order> PlaceAsync()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", price: 450m, stock: 5);
        var (_, placed) = await TestData.NewOrderManager(context).PlaceManualAsync(new ManualOrderDraft(
            "Ayşe Yılmaz", "0542 497 09 82", "ayse@example.com", "Cumhuriyet Mah. 12/3; kat 2", "İstanbul", "Kadıköy", null,
            PaymentMethod.KapidaOdeme, OrderSource.Telefon, [new ManualOrderLine("celik-tencere", 2)], ShippingFeeOverride: null, NotifyCustomer: false));
        return placed.Data!;
    }
}
