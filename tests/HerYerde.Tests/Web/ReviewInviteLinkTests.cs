using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Web;

/// <summary>D14 B5: değerlendirme davetindeki bağlantı yorum formunu sipariş numarasıyla doldurur; gönderilen
/// yorum doğrulanmış alıcı rozetiyle kaydedilir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReviewInviteLinkTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Davet_baglantisi_yorum_formunu_siparis_numarasiyla_doldurur_ve_rozet_verir()
    {
        await using var context = TestDb.NewContext();
        var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var cartManager = TestData.NewCartManager(context);
        var (_, cart) = await cartManager.GetOrCreateAsync(null);
        await cartManager.AddAsync(cart.Data!.Id, productId, null, 1);
        var (_, placed) = await TestData.NewOrderManager(context).PlaceAsync(cart.Data.Id, new OrderDraft(
            "Ayşe Yılmaz",
            "0542 497 09 82",
            "ayse@ornek.test",
            "Cumhuriyet Mah. 12/3",
            "İstanbul",
            "Kadıköy",
            null,
            PaymentMethod.KapidaOdeme));
        var orderNo = placed.Data!.OrderNo;

        var client = _factory.CreateNonRedirectingClient();
        var page = $"/urun/celik-tencere?siparis={orderNo}";
        var html = await (await client.GetAsync(page)).Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{orderNo}\"", html);

        var response = await HtmlForm.PostAsync(client, page, "/urun/celik-tencere/yorum", new Dictionary<string, string>
        {
            ["Rating"] = "5",
            ["Name"] = "Ayşe Y.",
            ["Comment"] = "Tencere tam beklediğim gibi çıktı.",
            ["OrderNo"] = orderNo
        });

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        await using var check = TestDb.NewContext();
        var review = Assert.Single(await new EfProductReviewDal(check).GetListAsync());
        Assert.Equal(orderNo, review.OrderNo);
        Assert.False(review.IsApproved);
    }

    [Fact]
    public async Task Baska_siparisin_numarasi_rozet_vermez()
    {
        await using var context = TestDb.NewContext();
        await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere", stock: 5);
        var client = _factory.CreateNonRedirectingClient();
        var page = "/urun/celik-tencere?siparis=HY-20260115-0099";

        var response = await HtmlForm.PostAsync(client, page, "/urun/celik-tencere/yorum", new Dictionary<string, string>
        {
            ["Rating"] = "4",
            ["Name"] = "Veli K.",
            ["Comment"] = "Bu ürünü almadım ama yorum yazıyorum.",
            ["OrderNo"] = "HY-20260115-0099"
        });

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        await using var check = TestDb.NewContext();
        var review = Assert.Single(await new EfProductReviewDal(check).GetListAsync());
        Assert.Null(review.OrderNo);
    }
}
