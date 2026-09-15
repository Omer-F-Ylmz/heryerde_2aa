using System.Net;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Concrete.EntityFramework;
using HerYerde.DataAccess.Concrete.EntityFramework.Contexts;
using HerYerde.Entities.Concrete;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

/// <summary>D13 A1: teslimden sonra 14 gün içinde kalem bazlı iade/değişim talebi; onay postası, teslim alınınca stok ve
/// kartta kalem tutarı kadar sağlayıcı iadesi, havalede elle iade işareti, değişimde yeni varyant stoğu.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReturnRequestTests : IAsyncLifetime
{
    private const string Iban = "TR33 0006 1005 1978 6457 8413 26";

    private readonly TestClock.MovableTimeProvider _clock = TestClock.Movable();
    private readonly FakePaymentProvider _provider = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Talep_teslimden_14_gun_sonra_400_icinde_201_sure_teslim_anindan_sayilir()
    {
        await using var context = TestDb.NewContext();
        var (order, items) = await PlaceHomeOrderAsync(context, PaymentMethod.KapidaOdeme, ("Çelik Tencere", "celik-tencere", 2));
        _clock.Advance(TimeSpan.FromDays(10));
        await DeliverAsync(context, order.Id);

        _clock.Advance(TimeSpan.FromDays(10));
        var (inside, _) = await Returns(context).RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));

        _clock.Advance(TimeSpan.FromDays(5));
        var (outside, result) = await Returns(context).RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));

        Assert.Equal(HttpStatusCode.Created, inside);
        Assert.Equal(HttpStatusCode.BadRequest, outside);
        Assert.Contains("14 gün", result.Message);
        Assert.Single(await new EfReturnRequestDal(context).GetListAsync());
    }

    [Fact]
    public async Task Talep_kalem_bazlidir_iade_edilebilir_adedi_asamaz()
    {
        await using var context = TestDb.NewContext();
        var (order, items) = await PlaceHomeOrderAsync(context, PaymentMethod.KapidaOdeme,
            ("Çelik Tencere", "celik-tencere", 2), ("Hasır Sepet", "hasir-sepet", 1));
        await DeliverAsync(context, order.Id);
        var returns = Returns(context);

        var (first, request) = await returns.RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));
        var (tooMany, _) = await returns.RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 2));
        var (other, _) = await returns.RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[1].Id, 1));
        var (wrongToken, _) = await returns.RequestAsync(order.OrderNo, Guid.NewGuid(), IadeDraft(items[1].Id, 1));

        Assert.Equal(HttpStatusCode.Created, first);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany);
        Assert.Equal(HttpStatusCode.Created, other);
        Assert.Equal(HttpStatusCode.NotFound, wrongToken);
        var line = Assert.Single(await new EfReturnRequestItemDal(context).GetListAsync(i => i.ReturnRequestId == request.Data!.Id));
        Assert.Equal(items[0].Id, line.OrderItemId);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(ReturnStatus.Bekliyor, request.Data!.Status);
    }

    [Fact]
    public async Task Onay_musteriye_iade_adresi_ve_kargo_bilgisi_postasi_kuyruga_yazar()
    {
        await using var context = TestDb.NewContext();
        var (order, items) = await PlaceHomeOrderAsync(context, PaymentMethod.KapidaOdeme, ("Çelik Tencere", "celik-tencere", 1));
        await DeliverAsync(context, order.Id);
        var (_, request) = await Returns(context).RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));

        var (status, _) = await Returns(context).ApproveAsync(request.Data!.Id);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(ReturnStatus.Onaylandi, (await new EfReturnRequestDal(context).GetAsync(r => r.Id == request.Data.Id))!.Status);
        var mail = Assert.Single(await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.ReturnApproved));
        Assert.Equal("ayse@example.com", mail.To);
        Assert.EndsWith(order.OrderNo, mail.Subject);
        Assert.Contains(TestData.ReturnAddress, mail.Body);
        Assert.Contains(TestData.ReturnCarrier, mail.Body);
    }

    [Fact]
    public async Task Ret_gerekce_ister_ve_gerekceyi_musteriye_yazar()
    {
        await using var context = TestDb.NewContext();
        var (order, items) = await PlaceHomeOrderAsync(context, PaymentMethod.KapidaOdeme, ("Çelik Tencere", "celik-tencere", 1));
        await DeliverAsync(context, order.Id);
        var (_, request) = await Returns(context).RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));

        var (empty, _) = await Returns(context).RejectAsync(request.Data!.Id, "  ");
        var (rejected, _) = await Returns(context).RejectAsync(request.Data.Id, "Ürün kullanılmış, ambalajı açık.");

        Assert.Equal(HttpStatusCode.BadRequest, empty);
        Assert.Equal(HttpStatusCode.OK, rejected);
        Assert.Equal(ReturnStatus.Reddedildi, (await new EfReturnRequestDal(context).GetAsync(r => r.Id == request.Data.Id))!.Status);
        var mail = Assert.Single(await new EfOutboxMessageDal(context).GetListAsync(m => m.Type == OutboxType.ReturnRejected));
        Assert.Contains("Ürün kullanılmış, ambalajı açık.", mail.Body);
    }

    [Fact]
    public async Task Kartli_sipariste_teslim_alininca_kalem_tutari_kadar_kismi_iade_stok_geri_ikinci_teslim_409()
    {
        await using var context = TestDb.NewContext();
        var (order, items) = await PlaceHomeOrderAsync(context, PaymentMethod.KrediKarti,
            ("Çelik Tencere", "celik-tencere", 2), ("Hasır Sepet", "hasir-sepet", 1));
        await MarkCardPaidAsync(context, order.Id);
        await DeliverAsync(context, order.Id);
        var returns = Returns(context);
        var (_, request) = await returns.RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));
        await returns.ApproveAsync(request.Data!.Id);
        var stockBefore = await TestData.ProductStockAsync(context, await ProductIdAsync(context, "celik-tencere"));

        var (received, _) = await returns.ReceiveAsync(request.Data.Id, "10.0.0.1");
        var (again, _) = await returns.ReceiveAsync(request.Data.Id, "10.0.0.1");

        Assert.Equal(HttpStatusCode.OK, received);
        Assert.Equal(HttpStatusCode.Conflict, again);
        var refund = Assert.Single(_provider.Refunds);
        Assert.Equal(450m, refund.Amount);
        Assert.True(refund.Partial);
        Assert.Equal("pay-kart", refund.PaymentId);
        var saved = (await new EfReturnRequestDal(context).GetAsync(r => r.Id == request.Data.Id))!;
        Assert.Equal(ReturnStatus.Tamamlandi, saved.Status);
        Assert.Equal(450m, saved.RefundAmount);
        Assert.NotNull(saved.RefundedAt);
        Assert.Equal(stockBefore + 1, await TestData.ProductStockAsync(context, await ProductIdAsync(context, "celik-tencere")));
        Assert.Equal(PaymentStatus.Basarili, (await new EfPaymentDal(context).GetAsync(p => p.OrderId == order.Id))!.Status);
    }

    [Fact]
    public async Task Tum_kalemler_donerse_kargo_dahil_tam_iade_ve_odeme_iade_durumuna_gecer()
    {
        await using var context = TestDb.NewContext();
        var (order, items) = await PlaceHomeOrderAsync(context, PaymentMethod.KrediKarti, ("Çelik Tencere", "celik-tencere", 1));
        await MarkCardPaidAsync(context, order.Id);
        await DeliverAsync(context, order.Id);
        var returns = Returns(context);
        var (_, request) = await returns.RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));
        await returns.ApproveAsync(request.Data!.Id);

        await returns.ReceiveAsync(request.Data.Id, "10.0.0.1");

        var refund = Assert.Single(_provider.Refunds);
        Assert.Equal(450m + TestData.ShippingFee, refund.Amount);
        Assert.False(refund.Partial);
        Assert.Equal(PaymentStatus.Iade, (await new EfPaymentDal(context).GetAsync(p => p.OrderId == order.Id))!.Status);
    }

    [Fact]
    public async Task Saglayici_iadeyi_reddederse_502_talep_ve_stok_degismez()
    {
        await using var context = TestDb.NewContext();
        var (order, items) = await PlaceHomeOrderAsync(context, PaymentMethod.KrediKarti, ("Çelik Tencere", "celik-tencere", 2));
        await MarkCardPaidAsync(context, order.Id);
        await DeliverAsync(context, order.Id);
        var returns = Returns(context);
        var (_, request) = await returns.RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));
        await returns.ApproveAsync(request.Data!.Id);
        var productId = await ProductIdAsync(context, "celik-tencere");
        var stockBefore = await TestData.ProductStockAsync(context, productId);
        _provider.RefundFails = true;

        var (status, _) = await returns.ReceiveAsync(request.Data.Id, "10.0.0.1");

        Assert.Equal(HttpStatusCode.BadGateway, status);
        await using var check = TestDb.NewContext();
        Assert.Equal(ReturnStatus.Onaylandi, (await new EfReturnRequestDal(check).GetAsync(r => r.Id == request.Data.Id))!.Status);
        Assert.Equal(stockBefore, await TestData.ProductStockAsync(check, productId));
    }

    [Fact]
    public async Task Havale_siparisinde_iade_iban_ister_teslim_alininca_elle_iade_isareti_bekler()
    {
        await using var context = TestDb.NewContext();
        var (order, items) = await PlaceHomeOrderAsync(context, PaymentMethod.HavaleEft, ("Çelik Tencere", "celik-tencere", 2));
        await DeliverAsync(context, order.Id);
        var returns = Returns(context);

        var (noIban, _) = await returns.RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1) with { Iban = null });
        var (_, request) = await returns.RequestAsync(order.OrderNo, order.AccessToken, IadeDraft(items[0].Id, 1));
        await returns.ApproveAsync(request.Data!.Id);
        var (received, _) = await returns.ReceiveAsync(request.Data.Id, "10.0.0.1");
        var afterReceive = (await new EfReturnRequestDal(context).GetAsync(r => r.Id == request.Data.Id))!;
        var (marked, _) = await returns.MarkRefundedAsync(request.Data.Id);

        Assert.Equal(HttpStatusCode.BadRequest, noIban);
        Assert.Equal(HttpStatusCode.OK, received);
        Assert.Empty(_provider.Refunds);
        Assert.Equal(ReturnStatus.TeslimAlindi, afterReceive.Status);
        Assert.Equal(450m, afterReceive.RefundAmount);
        Assert.Equal("TR330006100519786457841326", afterReceive.RefundIban);
        Assert.Null(afterReceive.RefundedAt);
        Assert.Equal(HttpStatusCode.OK, marked);
        var done = (await new EfReturnRequestDal(context).GetAsync(r => r.Id == request.Data.Id))!;
        Assert.Equal(ReturnStatus.Tamamlandi, done.Status);
        Assert.Equal(_clock.Moment, done.RefundedAt);
    }

    [Fact]
    public async Task Degisimde_yeni_varyant_stokta_olmali_teslim_alininca_eski_stok_doner_yeni_stok_duser()
    {
        await using var context = TestDb.NewContext();
        var (productId, mediumId) = await TestData.AddClothingProductAsync(context, "Şile Bezi Şalvar", "sile-salvar", "M", stock: 5);
        var large = new ProductVariant { ProductId = productId, Size = "L", Color = "Kiremit", Sku = "SILE-SALVAR-L", Stock = 0 };
        await new EfProductVariantDal(context).AddAsync(large);
        await new EfUnitOfWork(context).SaveChangesAsync();
        var (otherProduct, _) = await TestData.AddClothingProductAsync(context, "Keten Şalvar", "keten-salvar", "L", stock: 5);

        var cartManager = TestData.NewCartManager(context, _clock);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        await cartManager.AddAsync(cartId, productId, mediumId, 1);
        var (_, placed) = await TestData.NewOrderManager(context, _clock).PlaceAsync(cartId, Draft(PaymentMethod.KapidaOdeme));
        var order = placed.Data!;
        var item = Assert.Single(await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == order.Id));
        await DeliverAsync(context, order.Id);
        var returns = Returns(context);

        var (noStock, _) = await returns.RequestAsync(order.OrderNo, order.AccessToken, ExchangeDraft(item.Id, "SILE-SALVAR-L"));
        var (otherSku, _) = await returns.RequestAsync(order.OrderNo, order.AccessToken, ExchangeDraft(item.Id, "KETEN-SALVAR-L"));
        await SetVariantStockAsync(context, large.Id, 3);
        var (accepted, request) = await returns.RequestAsync(order.OrderNo, order.AccessToken, ExchangeDraft(item.Id, "SILE-SALVAR-L"));
        await returns.ApproveAsync(request.Data!.Id);
        var (received, _) = await returns.ReceiveAsync(request.Data.Id, "10.0.0.1");

        Assert.Equal(HttpStatusCode.Conflict, noStock);
        Assert.Equal(HttpStatusCode.BadRequest, otherSku);
        Assert.Equal(HttpStatusCode.Created, accepted);
        Assert.Equal(HttpStatusCode.OK, received);
        await using var check = TestDb.NewContext();
        Assert.Equal(5, (await new EfProductVariantDal(check).GetAsync(v => v.Id == mediumId))!.Stock);
        Assert.Equal(2, (await new EfProductVariantDal(check).GetAsync(v => v.Id == large.Id))!.Stock);
        var saved = (await new EfReturnRequestDal(check).GetAsync(r => r.Id == request.Data.Id))!;
        Assert.Equal(ReturnStatus.Tamamlandi, saved.Status);
        Assert.Equal(0m, saved.RefundAmount);
        Assert.Empty(_provider.Refunds);
        Assert.NotEqual(0, otherProduct);
    }

    [Fact]
    public async Task Musteri_iptali_Beklemede_ve_Onaylandida_olur_stok_doner_Hazirlaniyorda_409()
    {
        await using var context = TestDb.NewContext();
        var (pending, _) = await PlaceHomeOrderAsync(context, PaymentMethod.KapidaOdeme, ("Çelik Tencere", "celik-tencere", 2));
        var productId = await ProductIdAsync(context, "celik-tencere");
        var (confirmed, _) = await PlaceHomeOrderAsync(context, PaymentMethod.KapidaOdeme, ("Hasır Sepet", "hasir-sepet", 1));
        await TestData.NewOrderManager(context, _clock).ChangeStatusAsync(confirmed.Id, OrderStatus.Onaylandi);
        var (preparing, _) = await PlaceHomeOrderAsync(context, PaymentMethod.KapidaOdeme, ("Bambu Kase", "bambu-kase", 1));
        await TestData.NewOrderManager(context, _clock).ChangeStatusAsync(preparing.Id, OrderStatus.Onaylandi);
        await TestData.NewOrderManager(context, _clock).ChangeStatusAsync(preparing.Id, OrderStatus.Hazirlaniyor);
        var stockBefore = await TestData.ProductStockAsync(context, productId);

        var (pendingStatus, _) = await Returns(context).CancelByCustomerAsync(pending.OrderNo, pending.AccessToken, null, "10.0.0.1");
        var (confirmedStatus, _) = await Returns(context).CancelByCustomerAsync(confirmed.OrderNo, confirmed.AccessToken, null, "10.0.0.1");
        var (preparingStatus, _) = await Returns(context).CancelByCustomerAsync(preparing.OrderNo, preparing.AccessToken, null, "10.0.0.1");

        Assert.Equal(HttpStatusCode.OK, pendingStatus);
        Assert.Equal(HttpStatusCode.OK, confirmedStatus);
        Assert.Equal(HttpStatusCode.Conflict, preparingStatus);
        await using var check = TestDb.NewContext();
        var orders = new EfOrderDal(check);
        Assert.Equal(OrderStatus.IptalEdildi, (await orders.GetAsync(o => o.Id == pending.Id))!.Status);
        Assert.Equal(OrderStatus.IptalEdildi, (await orders.GetAsync(o => o.Id == confirmed.Id))!.Status);
        Assert.Equal(OrderStatus.Hazirlaniyor, (await orders.GetAsync(o => o.Id == preparing.Id))!.Status);
        Assert.Equal(stockBefore + 2, await TestData.ProductStockAsync(check, productId));
    }

    [Fact]
    public async Task Kartla_odenmis_siparisin_musteri_iptali_odemeyi_tam_iade_eder()
    {
        await using var context = TestDb.NewContext();
        var (order, _) = await PlaceHomeOrderAsync(context, PaymentMethod.KrediKarti, ("Çelik Tencere", "celik-tencere", 1));
        await MarkCardPaidAsync(context, order.Id);

        var (status, _) = await Returns(context).CancelByCustomerAsync(order.OrderNo, order.AccessToken, null, "10.0.0.1");

        Assert.Equal(HttpStatusCode.OK, status);
        var refund = Assert.Single(_provider.Refunds);
        Assert.Equal(order.Total, refund.Amount);
        Assert.False(refund.Partial);
        await using var check = TestDb.NewContext();
        Assert.Equal(PaymentStatus.Iade, (await new EfPaymentDal(check).GetAsync(p => p.OrderId == order.Id))!.Status);
        Assert.Equal(OrderStatus.IptalEdildi, (await new EfOrderDal(check).GetAsync(o => o.Id == order.Id))!.Status);
    }

    [Fact]
    public async Task Onayli_havalenin_musteri_iptali_iban_ister_elle_geri_odeme_isaretlenir()
    {
        await using var context = TestDb.NewContext();
        var (order, _) = await PlaceHomeOrderAsync(context, PaymentMethod.HavaleEft, ("Çelik Tencere", "celik-tencere", 1));
        await TestData.NewOrderManager(context, _clock).ApprovePaymentAsync(order.Id);

        var (noIban, _) = await Returns(context).CancelByCustomerAsync(order.OrderNo, order.AccessToken, null, "10.0.0.1");
        var (cancelled, _) = await Returns(context).CancelByCustomerAsync(order.OrderNo, order.AccessToken, Iban, "10.0.0.1");
        var marked = await Returns(context).MarkOrderRefundedAsync(order.Id);

        Assert.Equal(HttpStatusCode.BadRequest, noIban);
        Assert.Equal(HttpStatusCode.OK, cancelled);
        Assert.Equal(HttpStatusCode.OK, marked.Item1);
        await using var check = TestDb.NewContext();
        var saved = (await new EfOrderDal(check).GetAsync(o => o.Id == order.Id))!;
        Assert.Equal(OrderStatus.IptalEdildi, saved.Status);
        Assert.Equal(order.Total, saved.RefundDue);
        Assert.Equal("TR330006100519786457841326", saved.RefundIban);
        Assert.Equal(_clock.Moment, saved.RefundedAt);
    }

    private HerYerde.Business.Concrete.ReturnManager Returns(HerYerdeContext context) => TestData.NewReturnManager(context, _provider, _clock);

    private static ReturnDraft IadeDraft(int orderItemId, int quantity)
        => new(ReturnType.Iade, "Beklediğim gibi değil.", [new ReturnLine(orderItemId, quantity)], Iban, null);

    private static ReturnDraft ExchangeDraft(int orderItemId, string newSku)
        => new(ReturnType.Degisim, "Beden küçük geldi.", [new ReturnLine(orderItemId, 1, newSku)], null, null);

    private static OrderDraft Draft(PaymentMethod method) => new(
        "Ayşe Yılmaz", "05424970982", "ayse@example.com", "Cumhuriyet Mah. 12/3", "İstanbul", "Kadıköy", null, method);

    /// <summary>Stok takipli (10 adet) Ev ürünleriyle sipariş; kalemler sepetteki sırayla döner.</summary>
    private async Task<(Order Order, List<OrderItem> Items)> PlaceHomeOrderAsync(
        HerYerdeContext context,
        PaymentMethod method,
        params (string Name, string Slug, int Quantity)[] lines)
    {
        var cartManager = TestData.NewCartManager(context, _clock);
        var cartId = (await cartManager.GetOrCreateAsync(null)).Item2.Data!.Id;
        foreach (var (name, slug, quantity) in lines)
        {
            var productId = await new EfProductDal(context).GetAsync(p => p.Slug == slug) is { } existing
                ? existing.Id
                : await TestData.AddHomeProductAsync(context, name, slug, stock: 10);
            await cartManager.AddAsync(cartId, productId, variantId: null, quantity: quantity);
        }

        var (status, placed) = await TestData.NewOrderManager(context, _clock).PlaceAsync(cartId, Draft(method));
        Assert.Equal(HttpStatusCode.Created, status);
        var items = (await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == placed.Data!.Id)).OrderBy(i => i.Id).ToList();
        return (placed.Data!, items);
    }

    /// <summary>Kart ödemesi onaylanmış sayılır; stok sipariş anında düşmediği için burada elle düşülür.</summary>
    private static async Task MarkCardPaidAsync(HerYerdeContext context, int orderId)
    {
        var payment = (await new EfPaymentDal(context).GetTrackedAsync(p => p.OrderId == orderId))!;
        payment.Status = PaymentStatus.Basarili;
        payment.PaymentId = "pay-kart";
        foreach (var item in await new EfOrderItemDal(context).GetListAsync(i => i.OrderId == orderId))
        {
            var product = (await new EfProductDal(context).GetAsync(p => p.Slug == item.Sku))!;
            await new EfProductDal(context).TryDecrementStockAsync(product.Id, item.Quantity);
        }

        await new EfUnitOfWork(context).SaveChangesAsync();
    }

    private async Task DeliverAsync(HerYerdeContext context, int orderId)
    {
        var orders = TestData.NewOrderManager(context, _clock);
        var current = (await new EfOrderDal(context).GetAsync(o => o.Id == orderId))!.Status;
        if (current == OrderStatus.Beklemede)
        {
            Assert.Equal(HttpStatusCode.OK, (await orders.ChangeStatusAsync(orderId, OrderStatus.Onaylandi)).Item1);
        }

        Assert.Equal(HttpStatusCode.OK, (await orders.ChangeStatusAsync(orderId, OrderStatus.Hazirlaniyor)).Item1);
        Assert.Equal(HttpStatusCode.OK, (await orders.ChangeStatusAsync(orderId, OrderStatus.Kargoda, TestData.Carrier, "TR123")).Item1);
        Assert.Equal(HttpStatusCode.OK, (await orders.ChangeStatusAsync(orderId, OrderStatus.TeslimEdildi)).Item1);
    }

    private static async Task<int> ProductIdAsync(HerYerdeContext context, string slug)
        => (await new EfProductDal(context).GetAsync(p => p.Slug == slug))!.Id;

    private static async Task SetVariantStockAsync(HerYerdeContext context, int variantId, int stock)
    {
        await using var other = TestDb.NewContext();
        var variant = (await new EfProductVariantDal(other).GetTrackedAsync(v => v.Id == variantId))!;
        variant.Stock = stock;
        await new EfUnitOfWork(other).SaveChangesAsync();
    }
}
