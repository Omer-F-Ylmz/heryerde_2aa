using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => Problem(statusCode: 500, title: "Beklenmeyen bir hata oluştu.");

    /// <summary>UseStatusCodePagesWithReExecute hedefi: 404 markalı sayfa, diğerleri Problem.</summary>
    [Route("hata/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Hata(int code)
    {
        if (code == StatusCodes.Status404NotFound)
        {
            Response.StatusCode = code;
            return View("NotFound");
        }

        return Problem(statusCode: code);
    }
}
