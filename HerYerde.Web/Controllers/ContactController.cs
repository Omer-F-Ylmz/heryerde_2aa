using HerYerde.Business.Abstract;
using HerYerde.Entities.Concrete;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>/iletisim: kanallar ve form. Kayıt + mağaza e-postası tek işlemde; bot tuzağı dolu gelirse kayıt atılmaz.</summary>
public class ContactController(IContactService contactService, IConfiguration configuration) : Controller
{
    private string WhatsAppUrl => StoreCatalog.WhatsAppMessageUrl(configuration["Shop:WhatsApp"] ?? "https://wa.me/", "Merhaba, bir sorum var.");

    [HttpGet("iletisim")]
    public IActionResult Index() => View(new ContactPageVm(new ContactFormViewModel(), false, WhatsAppUrl));

    [HttpPost("iletisim")]
    public async Task<IActionResult> Send(ContactFormViewModel form, CancellationToken cancellationToken)
    {
        // Bot tuzağı: botu uyarmamak için gerçek gönderimle aynı yanıt, kayıt ve e-posta yok.
        if (!string.IsNullOrEmpty(form.Website))
        {
            return View("Index", new ContactPageVm(new ContactFormViewModel(), true, WhatsAppUrl));
        }

        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Index", new ContactPageVm(form, false, WhatsAppUrl));
        }

        await contactService.SendAsync(new ContactMessage
        {
            Name = form.Name.Trim(),
            Contact = form.Contact.Trim(),
            Subject = form.Subject,
            Message = form.Message.Trim()
        }, cancellationToken);

        return View("Index", new ContactPageVm(new ContactFormViewModel(), true, WhatsAppUrl));
    }
}
