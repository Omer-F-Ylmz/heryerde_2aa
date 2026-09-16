using System.Net;
using HerYerde.Business.Abstract;
using HerYerde.Business.Concrete;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>/hesap: siparişlerim, adres defteri, favoriler, yorumlarım, çeyiz listelerim, ayarlar ve hesabı silme.
/// Sayfalar kişisel veri taşıdığı için önbelleğe alınmaz (service worker da /hesap'a dokunmaz).</summary>
[Authorize(Policy = CustomerPolicy.Name)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AccountController(
    ICustomerService customers,
    IProductService products,
    IAdminAuditService auditService,
    IPrivateFileStorage files,
    IProvinceDirectory provinces,
    TimeProvider clock) : Controller
{
    private const string NoticeKey = "hesap-bildirim";

    private int CustomerId => CustomerPolicy.CustomerId(User)!.Value;

    [HttpGet("hesap")]
    public async Task<IActionResult> Orders(CancellationToken cancellationToken)
        => await PageAsync(async customer => new AccountOrdersViewModel
        {
            Customer = customer,
            Notice = Notice,
            Orders = await customers.GetOrdersAsync(customer.Id, cancellationToken)
        }, cancellationToken);

    [HttpGet("hesap/adresler")]
    public async Task<IActionResult> Addresses(CancellationToken cancellationToken)
        => await PageAsync(async customer => new AccountAddressesViewModel
        {
            Customer = customer,
            Notice = Notice,
            Addresses = await customers.GetAddressesAsync(customer.Id, cancellationToken)
        }, cancellationToken);

    [HttpGet("hesap/adresler/yeni")]
    public async Task<IActionResult> NewAddress(CancellationToken cancellationToken)
        => await PageAsync(customer => Task.FromResult(new AccountAddressFormViewModel
        {
            Customer = customer,
            Form = new AddressFormViewModel { FullName = customer.FullName ?? string.Empty, Phone = PhoneRules.Display(customer.Phone) }
        }), cancellationToken, "AddressForm");

    [HttpPost("hesap/adresler/yeni")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> NewAddress(AddressFormViewModel form, CancellationToken cancellationToken)
        => SaveAddressAsync(0, form, cancellationToken);

    [HttpGet("hesap/adresler/{id:int}")]
    public async Task<IActionResult> EditAddress(int id, CancellationToken cancellationToken)
    {
        var address = (await customers.GetAddressesAsync(CustomerId, cancellationToken)).FirstOrDefault(a => a.Id == id);
        if (address is null)
        {
            return NotFound();
        }

        return await PageAsync(customer => Task.FromResult(new AccountAddressFormViewModel
        {
            Customer = customer,
            Form = new AddressFormViewModel
            {
                Id = address.Id,
                Title = address.Title,
                FullName = address.FullName,
                Phone = PhoneRules.Display(address.Phone),
                Address = address.Address,
                City = address.City,
                District = address.District,
                IsDefault = address.IsDefault
            }
        }), cancellationToken, "AddressForm");
    }

    [HttpPost("hesap/adresler/{id:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EditAddress(int id, AddressFormViewModel form, CancellationToken cancellationToken)
        => SaveAddressAsync(id, form, cancellationToken);

    /// <summary>İl seçilince ilçe listesini tazeler (JS'siz de çalışır); kaydetmez.</summary>
    [HttpPost("hesap/adresler/ilce")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddressDistricts(AddressFormViewModel form, CancellationToken cancellationToken)
    {
        if (!provinces.DistrictsOf(form.City).Contains(form.District))
        {
            form.District = string.Empty;
        }

        ModelState.Clear();
        return await PageAsync(customer => Task.FromResult(new AccountAddressFormViewModel { Customer = customer, Form = form }), cancellationToken, "AddressForm");
    }

    [HttpPost("hesap/adresler/{id:int}/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken cancellationToken)
    {
        var (status, result) = await customers.DeleteAddressAsync(CustomerId, id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        TempData[NoticeKey] = result.Message;
        return Redirect("/hesap/adresler");
    }

    [HttpGet("hesap/favoriler")]
    public async Task<IActionResult> Favorites(CancellationToken cancellationToken)
        => await PageAsync(async customer =>
        {
            var favorites = await customers.GetFavoriteProductsAsync(customer.Id, cancellationToken);
            var (_, images) = await products.GetImagesForAsync(favorites.Select(p => p.Id).ToList(), cancellationToken);
            var now = clock.GetUtcNow().UtcDateTime;
            return new AccountFavoritesViewModel
            {
                Customer = customer,
                Notice = Notice,
                Cards = favorites.Select((p, index) => StoreCatalog.Card(p, images.Data!, now, lazy: index >= 4)).ToList()
            };
        }, cancellationToken);

    /// <summary>Kalp: JSON isteyen (site.js) sayfa yenilemeden durum alır; formla gelen dönüş adresine 303 ile döner.</summary>
    [HttpPost("hesap/favoriler/{productId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int productId, [FromForm(Name = CustomerPolicy.ReturnParameter)] string? returnUrl, CancellationToken cancellationToken)
    {
        var (status, result) = await customers.ToggleFavoriteAsync(CustomerId, productId, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        if (Request.GetTypedHeaders().Accept.Any(a => a.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            return Json(new { favorite = result.Data, message = result.Message });
        }

        Response.Headers.Location = CustomerPolicy.SafeReturn(returnUrl, "/hesap/favoriler");
        return StatusCode(StatusCodes.Status303SeeOther);
    }

    [HttpGet("hesap/yorumlar")]
    public async Task<IActionResult> Reviews(CancellationToken cancellationToken)
        => await PageAsync(async customer => new AccountReviewsViewModel
        {
            Customer = customer,
            Reviews = await customers.GetReviewsAsync(customer.Id, cancellationToken)
        }, cancellationToken);

    [HttpGet("hesap/ceyiz-listeleri")]
    public async Task<IActionResult> Registries(CancellationToken cancellationToken)
        => await PageAsync(async customer => new AccountRegistriesViewModel
        {
            Customer = customer,
            Registries = await customers.GetRegistriesAsync(customer.Id, cancellationToken)
        }, cancellationToken);

    [HttpGet("hesap/ayarlar")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken)
        => await PageAsync(customer => Task.FromResult(SettingsPage(customer)), cancellationToken);

    [HttpPost("hesap/ayarlar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(AccountSettingsViewModel form, CancellationToken cancellationToken)
    {
        var (status, result) = await customers.UpdateProfileAsync(CustomerId, new CustomerProfile(form.FullName, form.Phone, form.MarketingConsent), cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return await PageAsync(customer => Task.FromResult<AccountPageViewModel>(new AccountSettingsPageViewModel
            {
                Customer = customer,
                Form = form,
                ErrorMessage = result.Message
            }), cancellationToken, "Settings");
        }

        TempData[NoticeKey] = result.Message;
        return Redirect("/hesap/ayarlar");
    }

    [HttpPost("hesap/parola")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(AccountPasswordViewModel form, CancellationToken cancellationToken)
    {
        var (status, result) = ModelState.IsValid
            ? await customers.ChangePasswordAsync(CustomerId, form.CurrentPassword, form.NewPassword, cancellationToken)
            : (HttpStatusCode.BadRequest, new HerYerde.Core.Utilities.Results.ErrorDataResult<Customer>(
                ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage));
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return await PageAsync(customer =>
            {
                var page = SettingsPage(customer);
                page.PasswordError = result.Message;
                return Task.FromResult<AccountPageViewModel>(page);
            }, cancellationToken, "Settings");
        }

        await CustomerPolicy.SignInAsync(HttpContext, result.Data!);
        TempData[NoticeKey] = result.Message;
        return Redirect("/hesap/ayarlar");
    }

    [HttpPost("hesap/oturumlar/kapat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSessions(CancellationToken cancellationToken)
    {
        var (status, result) = await customers.RevokeSessionsAsync(CustomerId, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            return NotFound();
        }

        await CustomerPolicy.SignInAsync(HttpContext, result.Data!);
        TempData[NoticeKey] = result.Message;
        return Redirect("/hesap/ayarlar");
    }

    [HttpGet("hesap/sil")]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
        => await PageAsync(customer => Task.FromResult<AccountPageViewModel>(new AccountDeleteViewModel { Customer = customer }), cancellationToken);

    /// <summary>Onay için hesabın e-postası yeniden yazılır. Açık sipariş varsa 409; hesap silinince oturum kapanır.</summary>
    [HttpPost("hesap/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] string? email, CancellationToken cancellationToken)
    {
        var customer = await customers.GetAsync(CustomerId, cancellationToken);
        if (customer is null)
        {
            await CustomerPolicy.SignOutAsync(HttpContext);
            return Redirect("/");
        }

        if (CustomerManager.NormalizeEmail(email) != customer.Email)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new AccountDeleteViewModel { Customer = customer, ErrorMessage = "Onay için hesabınızın e-posta adresini aynen yazın." });
        }

        var (status, result) = await customers.DeleteAccountAsync(customer.Id, cancellationToken);
        if (status != HttpStatusCode.OK)
        {
            Response.StatusCode = (int)status;
            return View(new AccountDeleteViewModel { Customer = customer, ErrorMessage = result.Message });
        }

        var deletion = result.Data!;
        foreach (var file in deletion.Files)
        {
            files.Delete(file);
        }

        foreach (var order in deletion.AnonymizedOrders)
        {
            await auditService.ForgetDetailsAsync("sipariş", order.Id, CancellationToken.None);
            await auditService.WriteAsync(HttpContext, "kişisel veri anonimleştirildi (hesap silme)", "sipariş", order.Id);
        }

        await auditService.WriteAsync(HttpContext, "üye hesabını sildi", "üye", customer.Id,
            $"{PersonalDataMask.Email(customer.Email)} · {deletion.AnonymizedOrders.Count} sipariş");
        await CustomerPolicy.SignOutAsync(HttpContext);
        return Redirect("/");
    }

    private string? Notice => TempData[NoticeKey] as string;

    private static AccountSettingsPageViewModel SettingsPage(Customer customer) => new()
    {
        Customer = customer,
        Form = new AccountSettingsViewModel
        {
            FullName = customer.FullName,
            Phone = PhoneRules.Display(customer.Phone),
            MarketingConsent = customer.MarketingConsent
        }
    };

    private async Task<IActionResult> SaveAddressAsync(int id, AddressFormViewModel form, CancellationToken cancellationToken)
    {
        form.Id = id;
        if (ModelState.IsValid && !provinces.DistrictsOf(form.City).Contains(form.District))
        {
            ModelState.AddModelError(nameof(form.District), "İlçe seçtiğiniz ile ait değil.");
        }

        if (ModelState.IsValid)
        {
            var (status, result) = await customers.SaveAddressAsync(CustomerId, new CustomerAddress
            {
                Id = id,
                Title = form.Title,
                FullName = form.FullName,
                Phone = form.Phone,
                Address = form.Address,
                City = form.City,
                District = form.District,
                IsDefault = form.IsDefault
            }, cancellationToken);

            if (status == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            if (status is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                TempData[NoticeKey] = result.Message;
                return Redirect("/hesap/adresler");
            }

            form.ErrorMessage = result.Message;
        }

        Response.StatusCode = StatusCodes.Status400BadRequest;
        return await PageAsync(customer => Task.FromResult<AccountPageViewModel>(new AccountAddressFormViewModel { Customer = customer, Form = form }), cancellationToken, "AddressForm");
    }

    /// <summary>Hesap silinmiş ya da damgası düşmüşse (çerez doğrulaması yakalamadıysa) oturum kapanır.</summary>
    private async Task<IActionResult> PageAsync<T>(Func<Customer, Task<T>> build, CancellationToken cancellationToken, string? view = null)
        where T : AccountPageViewModel
    {
        var customer = await customers.GetAsync(CustomerId, cancellationToken);
        if (customer is null)
        {
            await CustomerPolicy.SignOutAsync(HttpContext);
            return Redirect(CustomerPolicy.LoginPath);
        }

        var model = await build(customer);
        return view is null ? View(model) : View(view, model);
    }
}
