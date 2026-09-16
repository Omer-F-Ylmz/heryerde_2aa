using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.DataAccess.Abstract;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HerYerde.Web.Controllers;

/// <summary>Çeyiz listesi. Sahip üye olmadan liste açar; yönetim anahtarı yolda değil sorguda/formda taşınır ki istek
/// loguna düşmesin. Paylaşılan adres 10 karakterlik tahmin edilemez parça; iki sayfa da indekslenmez.</summary>
public class GiftRegistryController(
    IGiftRegistryService registries,
    ICartService cartService,
    IProductService productService,
    IOptions<HerYerde.Business.ShopSettings> shop) : Controller
{
    private const string NoticeKey = "CeyizBildirim";
    private const int SearchTake = 8;

    [HttpGet("ceyizlistesi/yeni")]
    public IActionResult New() => View(new GiftRegistryNewVm(new GiftRegistryFormViewModel(), null));

    [HttpPost("ceyizlistesi/yeni")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GiftRegistryFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("New", new GiftRegistryNewVm(form, null));
        }

        var (status, result) = await registries.CreateAsync(Draft(form), cancellationToken);
        if (status != HttpStatusCode.Created)
        {
            Response.StatusCode = (int)status;
            return View("New", new GiftRegistryNewVm(form, result.Message));
        }

        TempData[NoticeKey] = result.Data!.Email is null
            ? "Listeniz açıldı. Bu sayfanın adresi yönetim bağlantınızdır: aşağıdan WhatsApp'la kendinize gönderin."
            : "Listeniz açıldı. Yönetim bağlantısını e-postanıza da gönderdik.";
        return Redirect(ManageUrl(result.Data.ManageToken));
    }

    /// <summary>Ürün araması "ara" parametresinde: "q" yerleşimdeki site arama kutusunu da doldururdu.</summary>
    [HttpGet("ceyizlistesi/yonet")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Manage(Guid t, string? ara, CancellationToken cancellationToken)
        => await registries.GetByTokenAsync(t, cancellationToken) is { } view
            ? View(await ManageVmAsync(view, ara, TempData[NoticeKey] as string, null, cancellationToken))
            : NotFound();

    [HttpPost("ceyizlistesi/yonet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid t, GiftRegistryFormViewModel form, CancellationToken cancellationToken)
    {
        // İletişim bilgisi bu formda değişmez; doğrulama yalnız düzenlenen alanlara bakar.
        ModelState.Remove(nameof(form.Phone));
        if (!ModelState.IsValid)
        {
            return await ManageErrorAsync(t, HttpStatusCode.BadRequest, "Liste bilgilerini kontrol edin.", cancellationToken);
        }

        var (status, result) = await registries.UpdateAsync(t, Draft(form), cancellationToken);
        return await AfterManageAsync(t, status, result.Message, cancellationToken);
    }

    [HttpPost("ceyizlistesi/yonet/ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(Guid t, int productId, int? variantId, int desiredQty, CancellationToken cancellationToken)
    {
        var (status, result) = await registries.AddItemAsync(t, productId, variantId, desiredQty, cancellationToken);
        return await AfterManageAsync(t, status, result.Message, cancellationToken);
    }

    [HttpPost("ceyizlistesi/yonet/adet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetQuantity(Guid t, int itemId, int desiredQty, CancellationToken cancellationToken)
    {
        var (status, result) = await registries.SetItemQuantityAsync(t, itemId, desiredQty, cancellationToken);
        return await AfterManageAsync(t, status, result.Message, cancellationToken);
    }

    [HttpPost("ceyizlistesi/yonet/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(Guid t, int itemId, CancellationToken cancellationToken)
    {
        var (status, result) = await registries.RemoveItemAsync(t, itemId, cancellationToken);
        return await AfterManageAsync(t, status, result.Message, cancellationToken);
    }

    [HttpGet("ceyizlistesi/{slug:length(10)}")]
    public async Task<IActionResult> Show(string slug, CancellationToken cancellationToken)
        => await registries.GetPublicAsync(slug, cancellationToken) is { } view
            ? View(new GiftRegistryPageVm(view))
            : NotFound();

    [HttpPost("ceyizlistesi/{slug:length(10)}/hediye")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Gift(string slug, int itemId, CancellationToken cancellationToken)
    {
        var view = await registries.GetPublicAsync(slug, cancellationToken);
        if (view is null || view.Lines.All(l => l.ItemId != itemId))
        {
            return NotFound();
        }

        var (_, cart) = await cartService.GetOrCreateAsync(CartCookie.Read(HttpContext), cancellationToken);
        CartCookie.Write(HttpContext, cart.Data!.Id);

        var (status, result) = await cartService.AddGiftAsync(cart.Data.Id, itemId, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View("Show", new GiftRegistryPageVm(view, result.Message));
        }

        return Redirect("/sepet");
    }

    private static GiftRegistryDraft Draft(GiftRegistryFormViewModel form)
        => new(form.OwnerName, form.Phone, form.Email, form.EventDate!.Value, form.Message, form.IsPublic);

    private static string ManageUrl(Guid token) => $"/ceyizlistesi/yonet?t={token}";

    private async Task<IActionResult> AfterManageAsync(Guid token, HttpStatusCode status, string message, CancellationToken cancellationToken)
    {
        if (status == HttpStatusCode.OK)
        {
            TempData[NoticeKey] = message;
            return Redirect(ManageUrl(token));
        }

        return await ManageErrorAsync(token, status, message, cancellationToken);
    }

    private async Task<IActionResult> ManageErrorAsync(Guid token, HttpStatusCode status, string message, CancellationToken cancellationToken)
    {
        if (await registries.GetByTokenAsync(token, cancellationToken) is not { } view)
        {
            return NotFound();
        }

        Response.StatusCode = (int)status;
        return View("Manage", await ManageVmAsync(view, null, null, message, cancellationToken));
    }

    private async Task<GiftRegistryManageVm> ManageVmAsync(
        GiftRegistryView view,
        string? query,
        string? notice,
        string? error,
        CancellationToken cancellationToken)
    {
        var results = new List<RegistrySearchResult>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var (_, page) = await productService.GetActiveAsync(
                new ProductQuery { Term = query.Trim(), Now = DateTime.UtcNow, Take = SearchTake },
                cancellationToken);
            var found = page.Data!.Items;
            var (_, images) = await productService.GetImagesForAsync(found.Select(i => i.Product.Id).ToList(), cancellationToken);

            foreach (var item in found)
            {
                var (_, variants) = await productService.GetVariantsAsync(item.Product.Id, cancellationToken);
                results.Add(new RegistrySearchResult(
                    item.Product,
                    images.Data!.Where(i => i.ProductId == item.Product.Id).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).FirstOrDefault()?.Url,
                    variants.Data!.Where(v => v.Stock > 0).OrderBy(v => v.Size).ThenBy(v => v.Color).ToList()));
            }
        }

        var baseUrl = shop.Value.BaseUrl.TrimEnd('/');
        var publicUrl = $"{baseUrl}/ceyizlistesi/{view.Registry.Slug}";
        return new GiftRegistryManageVm(
            view,
            publicUrl,
            StoreCatalog.WhatsAppMessageUrl("https://wa.me/", $"Çeyiz listem: {publicUrl}"),
            StoreCatalog.WhatsAppMessageUrl("https://wa.me/", $"Çeyiz listemin yönetim bağlantısı (kimseyle paylaşma): {baseUrl}{ManageUrl(view.Registry.ManageToken)}"),
            query,
            results,
            notice,
            error);
    }
}
