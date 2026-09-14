using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Kurumsal içerik: /hakkimizda statik görünüm, /sss docs/sss.md tablosundan.</summary>
public class ContentController(IConfiguration configuration) : Controller
{
    [HttpGet("hakkimizda")]
    public IActionResult About()
        => View(new AboutPageVm(StoreCatalog.WhatsAppMessageUrl(configuration["Shop:WhatsApp"] ?? "https://wa.me/", "Merhaba, HerYerde hakkında bir sorum var.")));

    [HttpGet("sss")]
    public IActionResult Faq() => View(FaqSource.Load());
}
