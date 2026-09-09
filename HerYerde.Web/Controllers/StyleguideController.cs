using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Tasarım sistemi rehberi; yalnız Development ortamında görünür, aksi hâlde 404.</summary>
[Route("styleguide")]
public class StyleguideController(IWebHostEnvironment environment) : Controller
{
    [HttpGet]
    public IActionResult Index() => environment.IsDevelopment() ? View() : NotFound();
}
