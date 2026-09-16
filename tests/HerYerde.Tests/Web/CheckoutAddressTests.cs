using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Enums;
using HerYerde.Web.Models;

namespace HerYerde.Tests.Web;

/// <summary>D14: ödeme formunda il/ilçe bağımlı seçimi. Liste yalnız yeni siparişin formunu doldurur;
/// kayıtlı siparişin il/ilçesi serbest metin kalır.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CheckoutAddressTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Il_listesi_81_il_ve_dogru_ilceleri_verir()
    {
        var directory = new ProvinceDirectory(RepoFile.PathOf("HerYerde.Web", "wwwroot"));

        Assert.Equal(81, directory.Names.Count);
        Assert.Contains("İstanbul", directory.Names);
        Assert.Equal(
            ["Başiskele", "Çayırova", "Darıca", "Derince", "Dilovası", "Gebze", "Gölcük", "İzmit", "Kandıra", "Karamürsel", "Kartepe", "Körfez"],
            directory.DistrictsOf("Kocaeli"));
        Assert.Contains("Çankaya", directory.DistrictsOf("Ankara"));
        Assert.DoesNotContain("Çankaya", directory.DistrictsOf("Kocaeli"));
        Assert.Empty(directory.DistrictsOf("Öyle Bir İl Yok"));
    }

    [Fact]
    public async Task Ilce_listesi_secilmemis_ilde_kapali_gelir()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);

        var html = await (await client.GetAsync("/odeme")).Content.ReadAsStringAsync();

        Assert.Contains("Önce il seçin", html);
        Assert.Contains("İl seçin", html);
        // İl listesi sunucudan gelir: JS kapalıyken de seçilebilir.
        Assert.Contains("Kocaeli", html);
    }

    [Fact]
    public async Task Jssiz_il_secimi_sayfayi_ilce_listesiyle_yeniler()
    {
        await using var context = TestDb.NewContext();
        var client = _factory.CreateNonRedirectingClient();
        await FillCartAsync(context, client);

        var response = await HtmlForm.PostAsync(client, "/odeme", "/odeme/ilce", new Dictionary<string, string>
        {
            ["FullName"] = "Ayşe Yılmaz",
            ["City"] = "Ankara",
            ["District"] = "Gebze"
        });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Çankaya", html);
        Assert.Contains("İlçe seçin", html);
        // Girilen ad korunur, ile uymayan ilçe düşer, sipariş açılmaz.
        Assert.Contains("Ayşe Yılmaz", html);
        Assert.DoesNotContain(">Gebze<", html);
        Assert.Empty(await new EfOrderDal(context).GetListAsync());
    }

    [Fact]
    public async Task Eski_siparisin_serbest_metin_il_ilcesi_duzenlemede_korunur()
    {
        await using var context = TestDb.NewContext();
        var manager = TestData.NewOrderManager(context);
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var cartManager = TestData.NewCartManager(context);
        var (_, cart) = await cartManager.GetOrCreateAsync(null);
        await cartManager.AddAsync(cart.Data!.Id, productId, null, 1);

        // Liste dışı, elle girilmiş eski değerler.
        var (_, placed) = await manager.PlaceAsync(cart.Data.Id, new OrderDraft(
            "Ayşe Yılmaz",
            "0542 497 09 82",
            null,
            "Cumhuriyet Mah. 12/3",
            "Konstantiniyye",
            "Eski İlçe",
            null,
            PaymentMethod.KapidaOdeme));
        var order = placed.Data!;

        var (status, _) = await manager.EditAsync(order.Id, new OrderEdit(
            "Cumhuriyet Mah. 14/5",
            order.City,
            order.District,
            order.Phone,
            null,
            new Dictionary<int, int>()));

        var saved = (await new EfOrderDal(context).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Konstantiniyye", saved.City);
        Assert.Equal("Eski İlçe", saved.District);
    }

    private static async Task FillCartAsync(HerYerdeContext context, HttpClient client)
    {
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        await HtmlForm.PostAsync(client, "/urun/celik-tencere", "/sepet/ekle", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(),
            ["quantity"] = "1"
        });
    }
}
